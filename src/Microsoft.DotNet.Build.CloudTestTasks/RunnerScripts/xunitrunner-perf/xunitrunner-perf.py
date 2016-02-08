#!/usr/bin/env py
import os
import os.path
import sys
sys.path.append(os.getenv('HELIX_WORK_ROOT'))
import copy
import json
import os
import re
import shutil
import socket
import subprocess
import urllib
import urlparse
import sys
import helix.azure_storage
import helix.depcheck
import helix.event
import helix.logs
import helix.saferequests
import xunit
import zip_script
from helix.cmdline import command_main
from helix.io import copy_tree_to, ensure_directory_exists, fix_path

log = helix.logs.get_logger()

def _write_output_path(file_path, settings):
    (scheme,_,path,_,_,_) = urlparse.urlparse(settings.output_uri, 'file')
    if scheme.lower() == 'file':
        path = urllib.url2pathname(path)
        output_path = os.path.join(path, os.path.basename(file_path))
        shutil.copy2(file_path, output_path)
        return output_path
    else:
        fc = helix.azure_storage.get_upload_client(settings)
        return fc.upload(file_path, os.path.basename(file_path))

def _prepare_execution_environment(settings, framework_in_tpa):
    workitem_dir = fix_path(settings.workitem_working_dir)
    correlation_dir = fix_path(settings.correlation_payload_dir)

    xunit_drop = os.path.join(correlation_dir, 'xunit')
    corerun_drop = os.path.join(correlation_dir, 'corerun')
    build_drop = os.path.join(correlation_dir)

    test_drop = os.path.join(workitem_dir)

    assembly_list = os.path.join(test_drop, settings.assembly_list)

    test_location = os.path.join(workitem_dir, 'execution')
    core_root = os.path.join(workitem_dir, 'core_root')

    ensure_directory_exists(test_location)
    ensure_directory_exists(core_root)

    log.info("Copying only test files from {} to {}".format(test_drop, test_location))
    copy_tree_to(test_drop, test_location)

    framework_target = core_root if framework_in_tpa else test_location
    log.info("Copying product binaries from {} to {}".format(build_drop, framework_target))
    _copy_package_files(assembly_list, build_drop, framework_target, core_root, test_location)

# used to copy the required xunit perf runner an its dependencies
# note that the perf runner will only be present for perf tests.
def _prepare_perf_execution_environment(settings, perf_runner):
    correlation_dir = fix_path(settings.correlation_payload_dir)
    test_location = os.path.join(fix_path(settings.workitem_working_dir), 'execution')

    xunit_perf_drop = os.path.join(correlation_dir, perf_runner)
    if not os.path.exists(xunit_perf_drop):
        raise Exception("Failed to find perf runner {} in directory {}.".format(perf_runner, correlation_dir))

    # get the first subdir in the root and append it to xunit_perf_drop
    buildSubdir = os.listdir(xunit_perf_drop)
    xunit_perf_drop = os.path.join(xunit_perf_drop, buildSubdir[0])
    xunit_perf_drop = os.path.join(xunit_perf_drop, "tools")
    log.info("Copying xunit perf drop from {} to {}.".format(xunit_perf_drop, test_location))
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.performance.run.exe"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.performance.metrics.dll"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.performance.logger.exe"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.runner.utility.desktop.dll"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "ProcDomain.dll"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "Microsoft.Diagnostics.Tracing.TraceEvent.dll"), test_location)
    # copy the architecture specific subdirectories
    archSubdirs = os.listdir(xunit_perf_drop)
    for archSubdir in archSubdirs:
        if os.path.isdir(os.path.join(xunit_perf_drop, archSubdir)):
            shutil.copytree(os.path.join(xunit_perf_drop, archSubdir), os.path.join(test_location, archSubdir))

def _copy_package_files(assembly_list, build_drop, test_location, coreroot_location, execution_location):
    log.info("Opening assembly list from {}".format(assembly_list))

    try:
        tempstr = open(assembly_list).read()
        assemblylist_obj = json.loads(tempstr)

        try:
            for assembly_name in assemblylist_obj["corerun"]:
                assembly_name = assembly_name.replace("/", os.path.sep)
                assembly_name = assembly_name.replace("\\", os.path.sep)
                assembly_path = os.path.join(build_drop, assembly_name)
                target_path = os.path.join(coreroot_location, os.path.basename(assembly_name))
                log.debug("Copying {} to {}".format(assembly_path, target_path))
                shutil.copy2(assembly_path, target_path)
            for assembly_name in assemblylist_obj["xunit"]:
                assembly_name = assembly_name.replace("/", os.path.sep)
                assembly_name = assembly_name.replace("\\", os.path.sep)
                assembly_path = os.path.join(build_drop, assembly_name)
                target_path = os.path.join(execution_location, os.path.basename(assembly_name))
                log.debug("Copying {} to {}".format(assembly_path, target_path))
                shutil.copy2(assembly_path, target_path)
            for assembly_name in assemblylist_obj["testdependency"]:
                assembly_name = assembly_name.replace("/", os.path.sep)
                assembly_name = assembly_name.replace("\\", os.path.sep)
                assembly_path = os.path.join(build_drop, assembly_name)
                target_path = os.path.join(test_location, os.path.basename(assembly_name))
                log.debug("Copying {} to {}".format(assembly_path, target_path))
                shutil.copy2(assembly_path, target_path)
        except:
            # failed to copy a product file
            log.error("Failed to copy product binary, dumping contents of '{}'".format(build_drop))
            for root, dirs, files in os.walk(build_drop):
                for file in files:
                    log.info(os.path.join(root, file))
            # this is a fatal error so let it propagate
            raise
    except:
        # failure to find assemblylist
        raise

def post_process_perf_results(settings, results_location, workitem_dir):
    # Use the xunit perf analysis exe from nuget package here
    log.info('Converting xml to csv')
    payload_dir = fix_path(os.getenv('HELIX_CORRELATION_PAYLOAD'))
    xmlconvertorpath = os.path.join(*[payload_dir, 'Microsoft.DotNet.xunit.performance.analysis', '1.0.0-alpha-build0028', 'tools', 'xunit.performance.analysis.exe'])
    if os.system(xmlconvertorpath+' -csv '+os.path.join(workitem_dir, 'results.csv')+' '+results_location) != 0:
        raise Exception('Failed to generate csv from result xml')

    perfscriptsdir = os.path.join(*[payload_dir, 'RunnerScripts', 'xunitrunner-perf'])
    # need to extract more properties from settings to pass to csvtojsonconvertor.py
    jsonPath = os.path.join(workitem_dir, settings.workitem_id+'.json')
    if os.system('%HELIX_PYTHONPATH% '+os.path.join(perfscriptsdir, 'csvjsonconvertor.py')+' --csvFile \"'+os.path.join(workitem_dir, 'results.csv')+'\" --jsonFile \"'+jsonPath+'\" --jobName "..." --jobDescription "..." --configName "..." --jobGroupName "..." --jobTypeName "Private" --username "CoreFx-Perf" --userAlias "deshank" --branch "ProjectK" --buildInfoName "1390881" --buildNumber "1390881" --machinepoolName "HP Z210 Workstation" --machinepoolDescription "Intel64 Family 6 Model 42 Stepping 7" --architectureName "AMD64" --manufacturerName "Intel" --microarchName "SSE2" --numberOfCores "4" --numberOfLogicalProcessors "8" --totalPhysicalMemory "16342" --osInfoName "Microsoft Windows 8.1 Pro" --osVersion "6.3.9600" --machineName "PCNAME" --machineDescription "Intel(R) Core(TM) i7-2600 CPU @ 3.40GHz"') != 0:
        raise Exception('Failed to generate json from csv file')

    perfsettings = copy.deepcopy(settings)
    with open(os.path.join(perfscriptsdir, 'xunitrunner-perf.json'), 'rb') as perfsettingsjson:
        # upload the json using perf-specific details
        perfsettingsjson = json.loads(perfsettingsjson.read())
        perfsettings.output_uri = perfsettingsjson['RootURI']
        perfsettings.output_write_token = perfsettingsjson['WriteToken']
        perfsettings.output_read_token = perfsettingsjson['ReadToken']
        _write_output_path(jsonPath, perfsettings)


def _run_xunit_from_execution(settings, test_dll, xunit_test_type, args):
    workitem_dir = fix_path(settings.workitem_working_dir)

    test_location = os.path.join(workitem_dir, 'execution')
    core_root = os.path.join(workitem_dir, 'core_root')
    results_location = os.path.join(workitem_dir, 'test_results.xml')

    event_client = helix.event.create_from_uri(settings.event_uri)

    log.info("Starting xunit against '{}'".format(test_dll))
    xunit_result = xunit.run_tests(
        settings,
        [test_dll],
        test_location,
        core_root,
        results_location,
        xunit_test_type,
        args
    )

    if xunit_test_type == xunit.XUNIT_CONFIG_PERF:
        # perf testing has special requirements on the test output file name.
        # make a copy of it in the expected location so we can report the result.
        perf_log = os.path.join(test_location, "latest-perf-build.xml")
        log.info("Copying {} to {}.".format(perf_log, results_location))
        shutil.copy2(perf_log, results_location)
        # archive the ETL file and upload it
        etl_file = os.path.join(test_location, "latest-perf-build.etl")
        etl_zip = os.path.join(test_location, "latest-perf-build.zip")
        log.info("Compressing {} into {}".format(etl_file, etl_zip))
        zip_script.zipFilesAndFolders(etl_zip, [etl_file], True, True)
        log.info("Uploading ETL from {}".format(etl_zip))
        _write_output_path(etl_zip, settings)

    log.info("XUnit exit code: {}".format(xunit_result))

    if os.path.exists(results_location):
        log.info("Uploading results from {}".format(results_location))

        with file(results_location) as result_file:
            test_count = 0
            for line in result_file:
                if '<assembly ' in line:
                    total_expression = re.compile(r'total="(\d+)"')
                    match = total_expression.search(line)
                    if match is not None:
                        test_count = int(match.groups()[0])
                    break

        post_process_perf_results(settings, results_location, workitem_dir)

        result_url = _write_output_path(results_location, settings)
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
        _report_error(settings)
    return xunit_result


def _report_error(settings):
    from traceback import format_tb, format_exc
    log.error("Error running xunit {}".format(format_exc()))
    (type, value, traceback) = sys.exc_info()
    event_client = helix.event.create_from_uri(settings.event_uri)
    formatted = format_tb(traceback)
    workitem_dir = fix_path(settings.workitem_working_dir)
    error_path = os.path.join(workitem_dir, 'error.log')
    lines = ['Unhandled error: {}\n{}'.format(value, formatted)]
    with open(error_path, 'w') as f:
        f.writelines(lines)
    error_url = _write_output_path(error_path, settings)
    log.info("Sending ToF test failure event")
    event_client.send(
        {
            'Type': 'XUnitTestFailure',
            'WorkItemId': settings.workitem_id,
            'WorkItemFriendlyName': settings.workitem_friendly_name,
            'CorrelationId': settings.correlation_id,
            'ErrorLogUri': error_url,
        }
    )



def run_tests(settings, test_dll, framework_in_tpa, perf_runner, args):
    try:
        log.info("Running on '{}'".format(socket.gethostname()))
        xunit_test_type = xunit.XUNIT_CONFIG_NETCORE
        _prepare_execution_environment(settings, framework_in_tpa)

        # perform perf test prep if required
        if perf_runner is not None:
            _prepare_perf_execution_environment(settings, perf_runner)
            xunit_test_type = xunit.XUNIT_CONFIG_PERF

        return _run_xunit_from_execution(settings, test_dll, xunit_test_type, args)
    except:
        _report_error(settings)
        # XUnit will now only return 0-4 for return codes.
        # so, use 5 to indicate a non-XUnit failure
        return 5

def main(args=None):
    def _main(settings, optlist, args):
        """
        Usage::
            xunitrunner
                [--config config.json]
                [--setting name=value]
                --dll Test.dll
        """
        optdict = dict(optlist)
        # check if a perf runner has been specified
        perf_runner = None
        if '--perf-runner' in optdict:
            perf_runner = optdict['--perf-runner']
        return run_tests(settings, optdict['--dll'], '--tpaframework' in optdict, perf_runner, args)

    return command_main(_main, ['dll=', 'tpaframework', 'perf-runner='], args)

if __name__ == '__main__':
    import sys
    sys.exit(main())

helix.depcheck.check_dependencies(__name__)