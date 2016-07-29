#!/usr/bin/env py
import os
import os.path
import sys
sys.path.append(os.getenv('HELIX_WORK_ROOT'))
import copy
"""
 This is a temporary solution to gather h/w info from machine
 the package is installed on end machines running perf
 However this runs on a limited number of platforms
 (but supports all the flavors we currently use for testing)
 moving forward we will need a truly cross-plat solution here
"""
from cpuinfo import cpuinfo
import json
import re
import shutil
import socket
import subprocess
import urllib
import urlparse
import helix.azure_storage
import helix.depcheck
import helix.event
import helix.logs
import helix.saferequests
import platform
import psutil
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
        try:
            fc = helix.azure_storage.get_upload_client(settings)
            url = fc.upload(file_path, os.path.basename(file_path))
            return url
        except ValueError, e:
            event_client = helix.event.create_from_uri(settings.event_uri)
            event_client.error(settings, "FailedUpload", "Failed to upload " + file_path + "after retry", None)

def _prepare_execution_environment(settings, framework_in_tpa, assembly_list_name):
    workitem_dir = fix_path(settings.workitem_working_dir)
    correlation_dir = fix_path(settings.correlation_payload_dir)

    xunit_drop = os.path.join(correlation_dir, 'xunit')
    corerun_drop = os.path.join(correlation_dir, 'corerun')
    build_drop = os.path.join(correlation_dir)

    test_drop = os.path.join(workitem_dir)

    assembly_list = os.path.join(test_drop, assembly_list_name)

    test_location = os.path.join(workitem_dir, 'execution')
    core_root = os.path.join(workitem_dir, 'core_root')

    ensure_directory_exists(test_location)
    ensure_directory_exists(core_root)

    log.info("Copying only test files from {} to {}".format(test_drop, test_location))
    copy_tree_to(test_drop, test_location)

    framework_target = core_root if framework_in_tpa else test_location
    log.info("Copying product binaries from {} to {}".format(build_drop, framework_target))
    _copy_package_files(assembly_list, build_drop, framework_target, core_root, test_location)

def _copy_files_to_dest(src, dest):
    for item in os.listdir(src):
        s = os.path.join(src, item)
        d = os.path.join(dest, item)
        if os.path.isfile(s):
            shutil.copy2(s, d)

def _prepare_linux_env_for_perf(correlation_dir, xunit_perf_drop, test_location, core_root):
    # copy over the cli runner and its dependencies for execution
    _copy_files_to_dest(xunit_perf_drop, test_location)
    xunit_perf_deps = os.path.join(correlation_dir, "Microsoft.DotNet.xunit.performance.run.core")
    if len(os.listdir(xunit_perf_deps)) > 1:
        log.info('Multiple directories found under '+xunit_perf_deps+' picking '+os.listdir(xunit_perf_deps)[0])
    xunit_perf_deps = os.path.join(xunit_perf_deps, os.listdir(xunit_perf_deps)[0])
    if os.path.exists(os.path.join(xunit_perf_deps, "lib", "netstandard1.3")):
        log.info('Using the netstandard1.3 folder which has the same deps in the dotnet folder just repackaged')
        xunit_perf_deps = os.path.join(xunit_perf_deps, "lib", "netstandard1.3")
    else:
        xunit_perf_deps = os.path.join(xunit_perf_deps, "lib", "dotnet")
    log.info('Copying xunit perf dependencies from '+xunit_perf_deps)
    _copy_files_to_dest(xunit_perf_deps, test_location)

    dotnet_cli_dir = os.path.join(correlation_dir, "dotnet_cli")

    # if local dotnet cli is already installed, skip
    if not os.path.exists(dotnet_cli_dir):
        # install dotnet cli locally
        log.info('Local dotnet cli install not found, launching the insallation script')
        dotnet_installer = os.path.join(correlation_dir, "RunnerScripts", "xunitrunner-perf", "ubuntu-dotnet-local-install.sh")
        log.info('Setting dotnet cli installation script at '+dotnet_installer+' as executable')
        helix.proc.run_and_log_output(("chmod 777 "+dotnet_installer).split(" "))
        log.info('Running script '+dotnet_installer)
        helix.proc.run_and_log_output((dotnet_installer+" -d "+dotnet_cli_dir+" -v "+os.path.join(correlation_dir, "RunnerScripts", "xunitrunner-perf", "DotNetCliVersion.txt")).split(" "))
    else:
        log.info('Local dotnet cli install found')

    # for dotnet to execute the dll we need the runtime files right next to the dll we need to test
    _copy_files_to_dest(core_root, test_location)

def _prepare_windows_env_for_perf(xunit_perf_drop, test_location):
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.performance.run.exe"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.performance.metrics.dll"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.performance.logger.exe"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "xunit.runner.utility.desktop.dll"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "ProcDomain.dll"), test_location)
    shutil.copy2(os.path.join(xunit_perf_drop, "Microsoft.Diagnostics.Tracing.TraceEvent.dll"), test_location)

# used to copy the required xunit perf runner an its dependencies
# note that the perf runner will only be present for perf tests.
def _prepare_perf_execution_environment(settings, perf_runner, use_dotnetcli):
    correlation_dir = fix_path(settings.correlation_payload_dir)
    test_location = os.path.join(fix_path(settings.workitem_working_dir), 'execution')
    core_root = os.path.join(settings.workitem_working_dir, 'core_root')
    os.environ['CORE_ROOT'] = core_root
    xunit_perf_drop = os.path.join(correlation_dir, perf_runner)
    if not os.path.exists(xunit_perf_drop):
        raise Exception("Failed to find perf runner {} in directory {}.".format(perf_runner, correlation_dir))

    # get the first subdir in the root and append it to xunit_perf_drop
    build_subdir = os.listdir(xunit_perf_drop)
    if len(build_subdir) > 1:
        log.info('Multiple directories found in '+xunit_perf_drop+' picking '+build_subdir[0])

    xunit_perf_drop = os.path.join(xunit_perf_drop, build_subdir[0])
    if perf_runner == 'Microsoft.DotNet.xunit.performance.runner.Windows':
        xunit_perf_drop = os.path.join(xunit_perf_drop, "tools")
    else:
        xunit_perf_drop = os.path.join(xunit_perf_drop, "lib", "netstandard1.3")

    log.info("Copying xunit perf drop from {} to {}.".format(xunit_perf_drop, test_location))

    if use_dotnetcli.lower() != 'true':
        _prepare_windows_env_for_perf(xunit_perf_drop, test_location)
    else:
        _prepare_linux_env_for_perf(correlation_dir, xunit_perf_drop, test_location, core_root)

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
        #failure to find assembly list
        raise

# does perf-specific tasks; converts results xml to csv and then to json, populates machine information and uploads json
def post_process_perf_results(settings, results_location, workitem_dir, xunit_test_type):
    # Use the xunit perf analysis exe from nuget package here
    log.info('Converting xml to csv')
    payload_dir = fix_path(os.getenv('HELIX_CORRELATION_PAYLOAD'))
    xmlconvertorpath = ''

    if xunit_test_type == xunit.XUNIT_CONFIG_PERF_WINDOWS:
        perf_lib_dir = os.path.join(payload_dir, 'Microsoft.DotNet.xunit.performance.analysis')
        if len(os.listdir(perf_lib_dir)) > 1:
            log.info('Multiple directories found under '+perf_lib_dir+' picking '+os.listdir(perf_lib_dir)[0])

        perf_analysis_version = os.listdir(perf_lib_dir)[0]
        xmlconvertorpath = os.path.join(payload_dir, 'Microsoft.DotNet.xunit.performance.analysis', perf_analysis_version, 'tools', 'xunit.performance.analysis.exe')
    elif xunit_test_type == xunit.XUNIT_CONFIG_PERF_LINUX:
        perf_lib_dir = os.path.join(payload_dir, 'Microsoft.DotNet.xunit.performance.analysis.cli')
        if len(os.listdir(perf_lib_dir)) > 1:
            log.info('Multiple directories found under '+perf_lib_dir+' picking '+os.listdir(perf_lib_dir)[0])

        perf_analysis_version = os.listdir(perf_lib_dir)[0]
        dotnet_cli_exec = os.path.join(settings.correlation_payload_dir, "dotnet_cli", "dotnet")
        _copy_files_to_dest(os.path.join(payload_dir, 'Microsoft.DotNet.xunit.performance.analysis.cli', perf_analysis_version, 'lib', 'netstandard1.3'), os.path.join(workitem_dir, 'execution'))
        xmlconvertorpath = dotnet_cli_exec + ' ' + os.path.join(workitem_dir, 'execution', 'Microsoft.DotNet.xunit.performance.analysis.cli.dll')
    else:
        log.error('Invalid xunit_test_type')
        return

    csvs_dir = os.path.join(workitem_dir, 'resultcsvs')
    os.mkdir(csvs_dir)
    xmlCmd = xmlconvertorpath+' -csv '+csvs_dir+' '+results_location
    if (helix.proc.run_and_log_output(xmlCmd.split(' '))) != 0:
        raise Exception('Failed to generate csv from result xml')

    log.info('Uploading the result csv files')
    for item in os.listdir(csvs_dir):
        if item.endswith('.csv'):
            _write_output_path(os.path.join(csvs_dir, item), settings)

    perfscriptsdir = os.path.join(payload_dir, 'RunnerScripts', 'xunitrunner-perf')
    perfsettingsjson = ''
    perfsettingsjsonfile = os.path.join(perfscriptsdir, 'xunitrunner-perf.json')
    with open(perfsettingsjsonfile, 'rb') as perfsettingsjson:
        # read the perf-specific settings
        perfsettingsjson = json.loads(perfsettingsjson.read())

    # need to extract more properties from settings to pass to csvtojsonconvertor.py
    jsonFileName = perfsettingsjson['TestProduct']+'-'+settings.workitem_id+'.json'
    jsonPath = os.path.join(workitem_dir, jsonFileName)
    jsonPath = jsonPath.encode('ascii', 'ignore')
    jsonArgsDict = dict()
    jsonArgsDict['--jobName'] = settings.correlation_id
    jsonArgsDict['--csvDir'] = csvs_dir
    jsonArgsDict['--jsonFile'] = jsonPath
    jsonArgsDict['--perfSettingsJson'] = perfsettingsjsonfile
    jsonArgs = [sys.executable, os.path.join(perfscriptsdir, 'csvjsonconvertor.py')]

    for key, value in jsonArgsDict.iteritems():
        jsonArgs.append(key)
        jsonArgs.append(str(value))

    if (helix.proc.run_and_log_output(jsonArgs)) != 0:
        raise Exception('Failed to generate json from csv file')

    # set info to upload result to perf-specific json container
    log.info('Uploading the results json')
    perfsettings = copy.deepcopy(settings)
    perfsettings.output_uri = perfsettingsjson['RootURI']
    perfsettings.output_write_token = perfsettingsjson['WriteToken']
    perfsettings.output_read_token = perfsettingsjson['ReadToken']
    # Upload json with rest of the results
    _write_output_path(jsonPath, settings)
    # Upload json to the perf specific container
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

    if xunit_test_type == xunit.XUNIT_CONFIG_PERF_WINDOWS or xunit_test_type == xunit.XUNIT_CONFIG_PERF_LINUX:
        # perf testing has special requirements on the test output file name.
        # make a copy of it in the expected location so we can report the result.
        perf_log = os.path.join(test_location, "latest-perf-build.xml")
        log.info("Copying {} to {}.".format(perf_log, results_location))
        shutil.copy2(perf_log, results_location)
        if xunit_test_type == xunit.XUNIT_CONFIG_PERF_WINDOWS:
            # only windows runs would generate the etl file
            # archive the ETL file and upload it
            etl_file = os.path.join(test_location, "latest-perf-build.etl")
            etl_zip = os.path.join(test_location, "latest-perf-build.zip")
            log.info("Compressing {} into {}".format(etl_file, etl_zip))
            zip_script.zipFilesAndFolders(etl_zip, [etl_file], True, True)
            log.info("Uploading ETL from {}".format(etl_zip))
            uploadlink = _write_output_path(etl_zip, settings)
            if uploadlink is not None:
                log.info('Blob uploaded at '+uploadlink)

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

        post_process_perf_results(settings, results_location, workitem_dir, xunit_test_type)

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



def run_tests(settings, test_dll, framework_in_tpa, assembly_list, perf_runner, use_dotnetcli, args):
    try:
        log.info("Running on '{}'".format(socket.gethostname()))
        xunit_test_type = xunit.XUNIT_CONFIG_NETCORE
        _prepare_execution_environment(settings, framework_in_tpa, assembly_list)

        # perform perf test prep if required
        if perf_runner is not None:
            _prepare_perf_execution_environment(settings, perf_runner, use_dotnetcli)
            if perf_runner == 'Microsoft.DotNet.xunit.performance.runner.Windows':
                xunit_test_type = xunit.XUNIT_CONFIG_PERF_WINDOWS
            else:
                xunit_test_type = xunit.XUNIT_CONFIG_PERF_LINUX

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
        assembly_list = None

        if '--perf-runner' in optdict:
            perf_runner = optdict['--perf-runner']
            if not os.path.exists(optdict['--dll']):
                dllpath = optdict['--dll']
                exepath = '.'.join(dllpath.split('.')[:-1])
                exepath = exepath + '.exe'
                if not os.path.exists(exepath):
                    raise Exception('No valid test dll or exe found')
                else:
                    optdict['--dll'] = exepath
        use_dotnetcli = optdict['--use-dotnetcli']
        #default to windows
        if use_dotnetcli == '':
            use_dotnetcli = 'false'


        if '--assemblylist' in optdict:
            assembly_list = optdict['--assemblylist']
            log.info("Using assemblylist parameter:"+assembly_list)
        else:
            assembly_list = os.getenv('HELIX_ASSEMBLY_LIST')
            log.info('Using assemblylist environment variable:'+assembly_list)
        return run_tests(settings, optdict['--dll'], '--tpaframework' in optdict, assembly_list, perf_runner, use_dotnetcli, args)

    return command_main(_main, ['dll=', 'tpaframework', 'perf-runner=', 'assemblylist=','use-dotnetcli='], args)

if __name__ == '__main__':
    import sys
    sys.exit(main())

helix.depcheck.check_dependencies(__name__)