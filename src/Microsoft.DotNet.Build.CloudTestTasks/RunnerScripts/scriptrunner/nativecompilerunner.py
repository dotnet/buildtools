#!/usr/bin/env py
import os.path
import json
import re
import uuid

import helix.depcheck
import helix.logs
import helix.proc
import helix.saferequests

from helix.cmdline import command_main
from helix.io import fix_path, zip_directory, add_file_to_zip
from helix_test_execution import HelixTestExecution
from helix.settings import settings_from_env
from helix.servicebusrepository import ServiceBusRepository
from helix.workitem import HelixWorkItem

log = helix.logs.get_logger()


def main(args=None):
    def _main(settings, optlist, args):
        """
        Usage::
            nativecompilerunner
                [--config config.json]
                [--setting name=value]
                --script
                [--args arg1 arg2...]
        """
        optdict = dict(optlist)
        log.info("BuildTools Helix Multipass Native Compilation Runner starting")

        if '--args' in optdict:
            script_arguments = optdict['--args']
            log.info("Script Arguments:"+script_arguments)

        if '--script' in optdict:
            script_to_execute = optdict['--script']
        else:
            log.error("Value for parameter '--script' is required")
            return -1

        if '--secondary_queue' in optdict:
            secondary_queue = optdict['--secondary_queue']
        if '--secondary_payload_dir' in optdict:
            secondary_payload_dir = optdict['--secondary_payload_dir']

        unpack_dir = fix_path(settings.workitem_payload_dir)

        execution_args = [os.path.join(unpack_dir, script_to_execute)] + args

        return_code = helix.proc.run_and_log_output(
            execution_args,
            cwd=unpack_dir,
            env=None
        )

        if return_code == 0:
            log.info("First stage of execution succeded.  Sending a new work item to " + secondary_queue)
            log.info("Will include contents of " + secondary_payload_dir)

            settings = settings_from_env()
            # load Client-specific settings
            config_path = os.path.join(settings.config_root, "ClientSettings.json")
            settings.__dict__.update(json.load(open(config_path)))
            service_bus_repository = ServiceBusRepository(settings.ServiceBusRoot,
                                                          settings.QueueId,
                                                          settings.LongPollTimeout,
                                                          settings.SAS,
                                                          settings.servicebus_retry_count,
                                                          settings.servicebus_retry_delay
                                                          )
            # This may eventually cause trouble if zips with identical names are somehow
            # included inside other payload zips.
            secondary_zip_path = os.path.join(settings.workitem_working_dir,
                                              settings.workitem_friendly_name + ".ilc.zip")

            zip_directory(secondary_zip_path, secondary_payload_dir)

            script_runner_dir=os.path.join(settings.correlation_payload_dir, "RunnerScripts\\scriptrunner")
            add_file_to_zip(secondary_zip_path,
                            os.path.join(script_runner_dir,
                                         "scriptrunner.py"),
                            script_runner_dir)

            log.info("Zipped into " + secondary_zip_path)

            upload_client = helix.azure_storage.BlobUploadClient(settings.output_uri,
                                                                 settings.output_write_token,
                                                                 settings.output_read_token)
            new_payload_uri = upload_client.upload(secondary_zip_path, settings.workitem_friendly_name + ".ilc.zip")

            log.info("Uploaded " + secondary_zip_path + " to " + new_payload_uri)

            # Prep the follow-up work item ...
            new_work_item = HelixWorkItem(
                correlation_id=settings.correlation_id,
                work_item_friendly_name=settings.workitem_friendly_name + ".Execution",
                command="%HELIX_PYTHONPATH% scriptrunner.py --script RunCompiledTest.cmd",
                results_output_uri=settings.output_uri + "/execution",
                results_output_write_token=settings.output_write_token,
                results_output_read_token=settings.output_read_token)

            new_work_item.WorkItemPayloadUris.append(new_payload_uri)

            if service_bus_repository.post_new_workitem(queue_id=secondary_queue,
                                                        work_item=new_work_item):
                log.info("Successfully queued new work item.")
            else:
                log.error("Failure to send to Service bus.")
                return -1

        else:
            log.error("Got non-zero exit code for first stage of execution.  Skipping further processing.")

        return return_code

    return command_main(_main, ['script=', 'args=', 'secondary_queue=', 'secondary_payload_dir='], args)

if __name__ == '__main__':
    import sys
    sys.exit(main())

helix.depcheck.check_dependencies(__name__)
