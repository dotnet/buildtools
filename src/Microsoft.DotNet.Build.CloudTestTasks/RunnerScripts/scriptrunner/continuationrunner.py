#!/usr/bin/env py

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

import os.path
import json
import platform
import re
import uuid

import helix.depcheck
import helix.logs
import helix.proc
import helix.saferequests

from helix.cmdline import command_main
from helix.io import fix_path, zip_directory, add_file_to_zip
from helix.platformutil import is_windows
from helix_test_execution import HelixTestExecution
from helix.settings import settings_from_env
from helix.servicebusrepository import ServiceBusRepository
from helix.workitem import HelixWorkItem

log = helix.logs.get_logger()


def main(args=None):
    def _main(settings, optlist, args):
        """
        Usage::
            continuationrunner
                [--config config.json]
                [--setting name=value]
                --script
                [--args arg1 arg2...]
        """
        optdict = dict(optlist)
        log.info("BuildTools Helix Continuation Runner starting")

        if '--args' in optdict:
            script_arguments = optdict['--args']
            log.info("Script Arguments: " + script_arguments)

        if '--script' in optdict:
            script_to_execute = optdict['--script']
        else:
            log.error("Value for parameter '--script' is required")
            return -1

        if '--next_queue' in optdict:
            next_queue = optdict['--next_queue']
        else:
            log.error("Need a secondary queue id to continue execution.")
            return -1
        if '--next_payload_dir' in optdict:
            next_payload_dir = optdict['--next_payload_dir']
        else:
            log.error("Need a secondary payload to continue execution.")
            return -1

        unpack_dir = fix_path(settings.workitem_payload_dir)

        execution_args = [os.path.join(unpack_dir, script_to_execute)] + args

        return_code = helix.proc.run_and_log_output(
            execution_args,
            cwd=unpack_dir,
            env=None
        )

        if return_code == 0:
            # currently there's no use for it, but here's where we'd choose to send out XUnit results
            # if desired at some point.
            log.info("First stage of execution succeded.  Sending a new work item to " + next_queue)
            log.info("Will include contents of " + next_payload_dir)

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
            # For now, we'll use ScriptRunner for this step. Eventually we'll want to either combine functionality
            # of the two into scriptrunner.py, OR parameterize which script is used (for the 2+ re-queue scenario)
            call_runcontinuation = "/RunnerScripts/scriptrunner/scriptrunner.py --script RunContinuation"
            if is_windows():
                continuation_command = "%HELIX_PYTHONPATH% %HELIX_CORRELATION_PAYLOAD%" + call_runcontinuation + ".cmd"
            else:
                continuation_command = "$HELIX_PYTHONPATH% $HELIX_CORRELATION_PAYLOAD" + call_runcontinuation + ".sh"

            # Prep the follow-up work item ...
            new_work_item = HelixWorkItem(
                correlation_id=settings.correlation_id,
                work_item_friendly_name=settings.workitem_friendly_name + ".Execution",
                command=continuation_command,
                results_output_uri=settings.output_uri + "/continuation",
                results_output_write_token=settings.output_write_token,
                results_output_read_token=settings.output_read_token)

            # This may eventually cause trouble if zips with identical names are somehow included inside
            # other payload zips. Chained continuation will be OK as there will be a new results
            # directory to upload to for each leg.
            new_workitem_payload_name = settings.workitem_friendly_name + ".continuation.zip"
            secondary_zip_path = os.path.join(settings.workitem_working_dir, new_workitem_payload_name)

            zip_directory(secondary_zip_path, next_payload_dir)
            log.info("Zipped into " + secondary_zip_path)

            # Upload the payloads for the job
            upload_client = helix.azure_storage.BlobUploadClient(settings.output_uri,
                                                                 settings.output_write_token,
                                                                 settings.output_read_token)
            new_payload_uri = upload_client.upload(secondary_zip_path, new_workitem_payload_name)
            new_work_item.WorkItemPayloadUris.append(new_payload_uri)

            # Current assumption: No need to reuse correlation payload, but bring supplemental (for scripts)
            # NOTE: We don't currently have a way to access the existing Uri, so reusing the payload from
            #       storage will involve plumbing that through or re-uploading it (can be huge)
            supplemental_payload_path = os.path.join(settings.work_root,
                                                     settings.correlation_id,
                                                     "work", "SupplementalPayload.zip")

            supplemental_payload_uri = upload_client.upload(supplemental_payload_path, "SupplementalPayload.zip")
            log.info("Uploaded " + secondary_zip_path + " to " + new_payload_uri)
            log.info("Uploaded SupplementalPayload.zip to " + supplemental_payload_uri)
            new_work_item.CorrelationPayloadUris.append(supplemental_payload_uri)

            if service_bus_repository.post_new_workitem(queue_id=next_queue,
                                                        work_item=new_work_item):
                log.info("Successfully queued new work item.")
            else:
                log.error("Failure to send to Service bus.")
                return -1

        else:
            log.error("Got non-zero exit code for first stage of execution.  Skipping further processing.")

        return return_code

    return command_main(_main, ['script=', 'args=', 'next_queue=', 'next_payload_dir='], args)

if __name__ == '__main__':
    import sys
    sys.exit(main())

helix.depcheck.check_dependencies(__name__)
