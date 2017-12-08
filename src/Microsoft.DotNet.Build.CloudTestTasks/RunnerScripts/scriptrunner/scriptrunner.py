#!/usr/bin/env py

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

import os.path
import re

import helix.depcheck
import helix.logs
import helix.proc
import helix.saferequests

from helix.cmdline import command_main
from helix.io import fix_path
from helix_test_execution import HelixTestExecution

log = helix.logs.get_logger()


def main(args=None):
    def _main(settings, optlist, args):
        """
        Usage::
            xunitrunner
                [--config config.json]
                [--setting name=value]
                --script=path
                [args]
        """
        optdict = dict(optlist)
        log.info("BuildTools Helix Script Runner v0.1 starting")
        if '--args' in optdict:
            script_arguments = optdict['--args']
            log.info("Script Arguments:"+script_arguments)

        script_to_execute = optdict['--script']
        unpack_dir = fix_path(settings.workitem_payload_dir)
        execution_args = [os.path.join(unpack_dir, script_to_execute)] + args

        test_executor = HelixTestExecution(settings)

        return_code = helix.proc.run_and_log_output(
            execution_args,
            cwd=unpack_dir,
            env=None
        )

        results_location = os.path.join(unpack_dir, 'testResults.xml')

        # In case testResults.xml was put somewhere else, try to find it anywhere in this directory before failing
        if not os.path.exists(results_location):
            for root, dirs, files in os.walk(settings.workitem_working_dir):
                for file_name in files:
                    if file_name == 'testResults.xml':
                        results_location = os.path.join(root, file_name)

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

            if settings.output_uri is not None:
                result_url = test_executor.upload_file_to_storage(results_location, settings)
            else:
                result_url = None;

            if (settings.event_uri is not None):
                event_client = helix.event.create_from_uri(settings.event_uri);
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
            if settings.output_uri is not None:
                test_executor.report_error(settings, failure_type="XUnitTestFailure")

        return return_code

    return command_main(_main, ['script=', 'args='], args)

if __name__ == '__main__':
    import sys
    sys.exit(main())

helix.depcheck.check_dependencies(__name__)
