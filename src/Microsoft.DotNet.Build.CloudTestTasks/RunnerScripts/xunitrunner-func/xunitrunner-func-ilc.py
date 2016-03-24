#!/usr/bin/env py
import json
import os.path
import re
import shutil

import helix.azure_storage
import helix.depcheck
import helix.event
import helix.logs
import helix.proc
import helix.saferequests

from helix.cmdline import command_main
from helix_test_execution import HelixTestExecution
from helix.io import ensure_directory_exists
from helix.settings import settings_from_env

log = helix.logs.get_logger()
event_client = helix.event.create_from_uri(settings_from_env().event_uri)


def _get_xunit_file_list(assembly_list, execution_location):
    log.info("Opening assembly list from {}".format(assembly_list))
    log.info("Only Xunit dependencies will be copied as this is a .NET Native compilation run")

    files_and_destinations = []
    try:
        assembly_list_obj = json.loads(open(assembly_list).read())
        try:
            for assembly_name in assembly_list_obj["xunit"]:
                assembly_name = assembly_name.replace("/", os.path.sep)
                assembly_name = assembly_name.replace("\\", os.path.sep)
                files_and_destinations.append((assembly_name, execution_location))
            return files_and_destinations
        except:
            log.error("Failed parsing " + assembly_list)
            # this is a fatal error
            raise
    except:
        # Failure to find assembly list
        raise


def _find_file(root, file_name):
    for (root, _, files) in os.walk(root):
        if file_name in files:
            return os.path.join(root, file_name)
    return None


def _create_reflection_directive_file(test_drop, working_source_path):
        rd_files = [f for f in os.listdir(test_drop) if f.endswith('.rd.xml')]
        rd_target_path = os.path.join(working_source_path, 'default.rd.xml')
        ensure_directory_exists(working_source_path)
        if len(rd_files) != 1:
            log.info("Creating default.rd.xml")
            with open(rd_target_path, 'w') as rd_xml:
                rd_xml.write(
                    """<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
                      <Application>
                        <Assembly Name="*Application*" Dynamic="Required All" />
                      </Application>
                    </Directives>
                    """
                )


def _run_ilc(settings, dll_name, ilc_path, working_source_path, working_target_path):
    ensure_directory_exists(working_target_path)

    # TODO: We may want to make the -buildtype pluggable and add the ability to add more args
    ilc_result = helix.proc.run_and_log_output([
        os.path.join(ilc_path, 'ilc.exe'),
        '-ExeName', 'xunit.console.netcore.exe',
        '-in', working_source_path,
        '-out', working_target_path,
        '-usedefaultpinvoke',
        '-buildtype', 'chk',
    ])

    if ilc_result != 0:
        log.error("Failed to run ILC.exe. Aborting test")
        event_client.error(
            settings,
            'XUnitTestFailure',
            'Failed to run ILC for {}'.format(dll_name),
        )
    return ilc_result


def _fixup_ilc_for_testing(settings, ilc_root, test_dll_path, working_target_path):
    # before we can run what we're natively compiling, we need to clean up the output a little bit.

    # Currently Native compilation needs there to be a file on disk before it will
    # run code that attempts to load a DLL, even if this has been compiled away into the
    # monolithic native DLL.
    dummy_test_dll = open(os.path.join(working_target_path, os.path.basename(test_dll_path)), 'w')
    dummy_test_dll.close()

    # Grab matching CRT DLL from the same place we get ILC, to ensure a compatible copy
    vcdll_source_path = _find_file(ilc_root, 'vcruntime140_app.dll')
    vcdll_path = os.path.join(working_target_path, os.path.basename(vcdll_source_path))
    log.info("Copy {} to {}".format(vcdll_source_path, vcdll_path))
    shutil.copy2(vcdll_source_path, vcdll_path)

    # CRT Dlls with _app on the end need to have their AppContainer bit un-set to run in a console app
    editbin_path = os.path.join(settings.correlation_payload_dir, 'editbin', 'editbin.exe')
    log.info("Running editbin: {} on ".format(editbin_path, vcdll_path))
    helix.proc.run_and_log_output([editbin_path, '/APPCONTAINER:NO', vcdll_path])


def _run_compiled_xunit_test(settings, test_executor, test_assembly, xunit_args):
    execution_directory = os.path.join(settings.workitem_working_dir, "postilc")
    results_xml_path = os.path.join(settings.workitem_working_dir, 'test_results.xml')
    log.info("Running native-compiled Core XUnit")

    dll_path = os.path.join(execution_directory, test_assembly)

    args = [
        os.path.join(execution_directory, 'xunit.console.netcore.exe'),
        dll_path,
        '-xml',
        results_xml_path
        ]
    args = args + xunit_args

    xunit_result = helix.proc.run_and_log_output(
        args,
        cwd=execution_directory,
        env=None
    )

    log.info("XUnit exit code: {}".format(xunit_result))

    if os.path.exists(results_xml_path):
        log.info("Uploading results from {}".format(results_xml_path))
        result_url = test_executor.upload_file_to_storage(results_xml_path, settings)

        with file(results_xml_path) as result_file:
            test_count = 0
            for line in result_file:
                if '<assembly ' in line:
                    total_expression = re.compile(r'total="(\d+)"')
                    match = total_expression.search(line)
                    if match is not None:
                        test_count = int(match.groups()[0])
                    break

        log.info("Sending completion event")
        event_client.send(
            {
                'Type': 'XUnitTestResult',
                'WorkItemId': settings.workitem_id,
                'WorkItemFriendlyName': settings.workitem_friendly_name,
                'CorrelationId': settings.correlation_id,
                'ResultsXmlUri': result_url,
                'TestCount': test_count,
            }
        )

    else:
        log.error("Error: No exception thrown, but XUnit results not created")
        test_executor.report_error(settings, failure_type="XUnitTestFailure")
    return xunit_result


def main(args=None):
    def _main(settings, optlist, args):
        """
        Usage::
            xunitrunner-func-ilc
                [--config config.json]
                [--setting name=value]
                [--tpaframework]
                [--assemblylist assemblylist.txt]
                [--xunit-test-type type]
                --dll Test.dll
        """
        optdict = dict(optlist)
        log.info("BuildTools Functional .NET Native Helix Runner v0.1 starting")

        if '--assemblylist' in optdict:
            assembly_list = optdict['--assemblylist']
            log.info("Using assemblylist parameter:"+assembly_list)
        else:
            assembly_list = os.getenv('HELIX_ASSEMBLY_LIST')
            log.info("Using assemblylist environment variable:"+assembly_list)

        log.info("WorkItem Friendly Name: " + settings.workitem_friendly_name)
        log.info("HelixJobRepro Repro Tool command:")
        log.info("HelixJobRepro.exe -j " + settings.correlation_id +
                 " -w " + settings.workitem_friendly_name +
                 " -a " + assembly_list)

        test_assembly = optdict['--dll']

        test_executor = HelixTestExecution(settings)

        ilc_root = os.path.dirname(_find_file(settings.correlation_payload_dir, 'ilc.exe'))
        preilc_dir = os.path.join( settings.workitem_working_dir, "execution")
        execution_dir = os.path.join(settings.workitem_working_dir, "postilc")
        try:
            # Copy just XUnit over... we may want to share payloads between IL and ILC runs
            file_tuples = _get_xunit_file_list(os.path.join(settings.workitem_payload_dir, assembly_list),
                                               None)
            test_executor.copy_file_list(settings.correlation_payload_dir,
                                         file_tuples,
                                         preilc_dir)
            # Dump out a default rd.xml to tell it to build everything in the test DLL into the native DLL
            _create_reflection_directive_file(settings.workitem_working_dir, preilc_dir)
            # Perform native compilation
            ilc_result = _run_ilc(settings, test_assembly, ilc_root, preilc_dir, execution_dir)
            # Only continue if ILC succeeded
            # TODO: Could introduce toolchain "expected error" tests by providing an arg here for the expected
            #       exit code.
            if ilc_result == 0:
                # Make a dummy assembly file, copy CRT file in,and flip APPCONTAINER bit on the CRT dll
                _fixup_ilc_for_testing(settings, ilc_root, test_assembly, execution_dir)
                # Run the tests, send off results
                xunit_result = _run_compiled_xunit_test(settings, test_executor, test_assembly, args)
            else:
                return ilc_result
        except Exception:
            test_executor.report_error(settings, "XUnitTestFailure")
            raise
        return xunit_result

    return command_main(_main, ['dll=', 'tpaframework', 'perf-runner=', 'assemblylist=', 'xunit-test-type='], args)

if __name__ == '__main__':
    import sys
    sys.exit(main())

helix.depcheck.check_dependencies(__name__)
