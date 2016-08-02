#!/usr/bin/env py
import copy
import helix.depcheck
import helix.logs
import helix.proc
import helix.azure_storage
import helix.event
from helix.io import fix_path
from helix.settings import settings_from_env
import json
import os.path
import traceback
import sys
import helix.logs
from helix.cmdline import command_main

log = helix.logs.get_logger()

def _upload_file(file_path, settings):
    try:
        event_client = helix.event.create_from_uri(settings.event_uri)
        fc = helix.azure_storage.get_upload_client(settings)
        url = fc.upload(file_path, os.path.basename(file_path))
        log.info('Uploaded file at location {}'.format(url))
    except ValueError, e:
        event_client = helix.event.create_from_uri(settings.event_uri)
        event_client.error(settings, "FailedUpload", "Failed to upload " + file_path + "after retry", None)

def post_process_perf_results(opts):
    csvs_dir = opts['--csvDir']
    if not os.path.exists(csvs_dir):
        log.error('Not able to find csvs directory {}'.format(csvs_dir))
        return -1

    settings = settings_from_env()
    perf_settings_json = ''
    perf_settings_json_file = os.path.join(fix_path(settings.correlation_payload_dir), 'RunnerScripts', 'xunitrunner-perf', 'xunitrunner-perf.json')
    with open(perf_settings_json_file) as perf_settings_json:
        # read the perf-specific settings
        perf_settings_json = json.loads(perf_settings_json.read())

    json_file = os.path.join(settings.workitem_working_dir, perf_settings_json['TestProduct']+'-'+settings.workitem_id+'.json')
    json_file = json_file.encode('ascii', 'ignore')

    json_cmd = sys.executable+' '+os.path.join(fix_path(settings.correlation_payload_dir), 'RunnerScripts', 'xunitrunner-perf', 'csvjsonconvertor.py')+\
               ' --jobName '+settings.correlation_id+' --csvDir '+csvs_dir+' --jsonFile '+json_file+' --perfSettingsJson '+perf_settings_json_file
    try:
        return_code = helix.proc.run_and_log_output(
                json_cmd.split(' '),
                cwd=os.path.join(fix_path(settings.workitem_working_dir), 'execution'),
                env=None
            )
    except:
            log.error("Exception when running the installation scripts: " + traceback.format_exc())

    for item in os.listdir(csvs_dir):
        if item.endswith('.csv'):
            csv_file = os.path.join(csvs_dir, item)
            log.info('Uploading {}'.format(csv_file))
            _upload_file(csv_file, settings)
            result_url = _upload_file(csv_file, settings)
            log.info('Location {}'.format(result_url))

    # Upload json with rest of the results
    log.info('Uploaded {} to results container'.format(json_file))
    result_url = _upload_file(json_file, settings)
    log.info('Location {}'.format(result_url))

    # create deep copy and set perf container keys

    perf_settings = copy.deepcopy(settings)
    perf_settings.output_uri = perf_settings_json['RootURI']
    perf_settings.output_write_token = perf_settings_json['WriteToken']
    perf_settings.output_read_token = perf_settings_json['ReadToken']
    log.info('Uploaded {} to perf container'.format(json_file))
    # Upload json to the perf specific container
    result_url = _upload_file(json_file, perf_settings)
    log.info('Location {}'.format(result_url))


def main(args=None):
    def _main(settings, optlist, _):
        """
            Usage::
                csv_to_json.py
                    --csvDir dir where csvs can be found
        """


        opts = dict(optlist)
        return post_process_perf_results(opts)

    return command_main(_main, ['csvDir='], args)

if __name__ == '__main__':
    sys.exit(main())