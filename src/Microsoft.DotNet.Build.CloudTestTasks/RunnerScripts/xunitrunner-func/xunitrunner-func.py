#!/usr/bin/env py
import json
import os.path

import helix.azure_storage
import helix.depcheck
import helix.event
import helix.logs
import helix.saferequests

from helix.cmdline import command_main
from xunit_execution import XUnitExecution

log = helix.logs.get_logger()


def _create_package_file_list(assembly_list, execution_location, coreroot_location, framework_in_tpa=False):
    log.info("Opening assembly list from {}".format(assembly_list))
    framework_target = coreroot_location if framework_in_tpa else execution_location

    if framework_in_tpa:
        log.info("Framework assemblies will be copied to be in TPA list.")
    else:
        log.info("Framework assemblies will be copied to execution directory (not in TPA list.)")

    files_and_destinations = []
    try:
        assembly_list_obj = json.loads(open(assembly_list).read())

        try:
            for assembly_name in assembly_list_obj["corerun"]:
                assembly_name = assembly_name.replace("/", os.path.sep)
                assembly_name = assembly_name.replace("\\", os.path.sep)
                files_and_destinations.append((assembly_name, coreroot_location))
            for assembly_name in assembly_list_obj["xunit"]:
                assembly_name = assembly_name.replace("/", os.path.sep)
                assembly_name = assembly_name.replace("\\", os.path.sep)
                files_and_destinations.append((assembly_name, execution_location))
            for assembly_name in assembly_list_obj["testdependency"]:
                assembly_name = assembly_name.replace("/", os.path.sep)
                assembly_name = assembly_name.replace("\\", os.path.sep)
                files_and_destinations.append((assembly_name, framework_target))
            return files_and_destinations
        except:
            log.error("Failed parsing " + assembly_list)
            # this is a fatal error so let it propagate
            raise
    except:
        # Failure to find assembly list
        raise


def main(args=None):
    def _main(settings, optlist, args):
        """
        Usage::
            xunitrunner
                [--config config.json]
                [--setting name=value]
                [--tpaframework]
                [--assemblylist assemblylist.txt]
                [--xunit-test-type type]
                --dll Test.dll
        """
        optdict = dict(optlist)
        log.info("BuildTools Functional Helix Runner v0.1 starting")
        if '--assemblylist' in optdict:
            assembly_list = optdict['--assemblylist']
            log.info("Using assemblylist parameter:"+assembly_list)
        else:
            assembly_list = os.getenv('HELIX_ASSEMBLY_LIST')
            log.info("Using assemblylist environment variable:"+assembly_list)

        test_assembly = optdict['--dll']

        xunit_test_type = XUnitExecution.XUNIT_CONFIG_NETCORE
        if '--xunit-test-type' in optdict:
            xunit_test_type = optdict['--xunit-test-type']
        if xunit_test_type == XUnitExecution.XUNIT_CONFIG_DESKTOP and os.name != 'nt':
            raise Exception("Error: Cannot run desktop .NET Framework XUnit on non windows platforms")

        # Currently this will automatically create the default "execution" folder and move contents of the
        # work item to it.  If needed, we could separate this out.
        xunit_execute = XUnitExecution(settings)

        # This runner handles the "original" AssemblyList format by contructing tuples to copy and then
        # calling HelixTestExecution.copy_file_list.  Once assembly list formatting is changed in the
        # source that this runner lives with, can convert to simply calling HelixTestExecution.copy_package_files

        # Eventually --tpaframework ought to be removed entirely from the runner, but leaving it in here
        # in case it's used.  If specified, we copy framework assemblies into CORE_ROOT to get different trust
        file_tuples = _create_package_file_list(assembly_list, "execution", "core_root", '--tpaframework' in optdict)
        xunit_execute.test_execution.copy_file_list(settings.correlation_payload_dir,
                                                    file_tuples,
                                                    settings.workitem_working_dir)

        # Custom runners put stuff here to do things before execution begins,
        xunit_result = xunit_execute.run_xunit(settings, test_assembly, xunit_test_type, args)
        # or here, to do any post-run work they want to.

        return xunit_result

    return command_main(_main, ['dll=', 'tpaframework', 'perf-runner=', 'assemblylist=', 'xunit-test-type='], args)

if __name__ == '__main__':
    import sys
    sys.exit(main())

helix.depcheck.check_dependencies(__name__)
