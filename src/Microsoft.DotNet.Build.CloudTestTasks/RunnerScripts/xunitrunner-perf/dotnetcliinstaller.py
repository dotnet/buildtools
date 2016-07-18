#!/usr/bin/env py
from helix.io import fix_path
import helix.logs
import helix.proc
from helix.settings import settings_from_env
import os.path
import traceback

log = helix.logs.get_logger()

def prepare_linux_for_perf():
    settings = settings_from_env()
    correlation_dir = fix_path(settings.correlation_payload_dir)
    dotnet_cli_dir = os.path.join(correlation_dir, "perf.dotnetcli")
    dotnet_cli = os.path.join(dotnet_cli_dir, "dotnet")
    # if local dotnet cli is already installed, skip
    if not os.path.exists(dotnet_cli):
        # install dotnet cli locally
        log.info('Local dotnet cli install not found, launching the installation script')
        dotnet_installer = os.path.join(correlation_dir, "RunnerScripts", "xunitrunner-perf", "ubuntu-dotnet-local-install.sh")
        try:
            log.info('Setting dotnet cli installation script at '+dotnet_installer+' as executable')
            helix.proc.run_and_log_output(("chmod 777 "+dotnet_installer).split(" "))
            log.info('Running script '+dotnet_installer)
            helix.proc.run_and_log_output((dotnet_installer+" -d "+dotnet_cli_dir+" -v "+os.path.join(correlation_dir, "RunnerScripts", "xunitrunner-perf", "DotNetCliVersion.txt")).split(" "))
        except:
            log.error("Exception when running the installation scripts: " + traceback.format_exc())
    else:
        log.info('Local dotnet cli install found')

def main(args=None):
    prepare_linux_for_perf()

if __name__ == '__main__':
    import sys
    sys.exit(main())
