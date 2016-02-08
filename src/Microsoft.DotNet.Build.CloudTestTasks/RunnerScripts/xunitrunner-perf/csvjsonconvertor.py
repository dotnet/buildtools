import csv
import getopt
import json
import os
import os.path
import serialobj
import sys
sys.path.append(os.getenv('HELIX_SCRIPT_ROOT'))
import helix.logs
from helix.cmdline import command_main

log = helix.logs.get_logger()

def read_csv(csvFile):
    log.info('Reading '+csvFile)
    if not os.path.exists(csvFile):
        raise Exception(csvFile+' could not be found for conversion')

    csvdict = dict()
    with open(csvFile, 'rb') as csvfile:
        reader = csv.reader(csvfile)
        # sample csv row looks like runName, suiteName, testCaseName, resultValue
        for row in reader:
            if row[0] not in csvdict:
                csvdict[row[0]] = dict()

            if row[1] not in csvdict.get(row[0]):
                csvdict.get(row[0])[row[1]] = dict()
            if row[2] not in csvdict.get(row[0]).get(row[1]):
                csvdict.get(row[0]).get(row[1])[row[2]] = list()

                csvdict.get(row[0]).get(row[1]).get(row[2]).append(row[3])

    return csvdict

def generate_json(opts, csvdict):

    log.info('Attempting to generate '+opts['--jsonFile'])
    runTests = list()

    # iterate runs
    for run, suites in csvdict.iteritems():
        runTest = serialobj.Test()
        runTest.testName = run
        suiteTests = list()
        # iterate suites
        for suite, testcases in suites.iteritems():
            suiteTest = serialobj.Test()
            suiteTest.testName = suite
            tests = list()
            # iterate tests
            for testcase, resultvalues in testcases.iteritems():
                test = serialobj.Test()
                results = list()
                result = serialobj.Result()

                # generate measurement result test object per iteration
                measurements = list()
                measurement = serialobj.Measurement()
                measurement.iterValues = resultvalues
                measurement.measurementType = 'execution_time'
                measurements.append(measurement)
                result.measurements = measurements
                results.append(result)
                test.results = results
                test.testName = testcase
                test.machine = serialobj.Machine()
                test.machine.machineName = opts['--machineName']
                test.machine.machineDescription = opts['--machineDescription']
                tests.append(test)

            suiteTest.tests = tests
            suiteTests.append(suiteTest)

        runTest.tests = suiteTests
        runTest.machine = serialobj.Machine()
        runTests.append(runTest)

    run = serialobj.Run()
    run.testList = runTests

    machinepool = serialobj.MachinePool()

    architecture = serialobj.Architecture()
    architecture.architectureName = opts['--architectureName']
    machinepool.architecture = architecture

    manufacturer = serialobj.Manufacturer()
    manufacturer.manufacturerName = opts['--manufacturerName']
    machinepool.manufacturer = manufacturer

    microarch = serialobj.MicroArch()
    microarch.microarchName = opts['--microarchName']

    osInfo = serialobj.OSInfo()
    osInfo.osInfoName = opts['--osInfoName']
    osInfo.osVersion = opts['--osVersion']

    machinepool.osInfo = osInfo
    machinepool.microarch = microarch
    machinepool.NumberOfCores = opts['--numberOfCores']
    machinepool.NumberOfLogicalProcessors = opts['--numberOfLogicalProcessors']
    machinepool.TotalPhysicalMemory = opts['--totalPhysicalMemory']
    machinepool.machinepoolName = opts['--machinepoolName']
    machinepool.machinepoolDescription = opts['--machinepoolDescription']
    machinepool.TotalPhysicalMemory = opts['--totalPhysicalMemory']
    run.machinepool = machinepool

    config = serialobj.Config()
    config.configName = opts['--configName']
    run.config = config


    runs = list()
    runs.append(run)

    job = serialobj.Job()
    job.Runs = runs

    user = serialobj.User()
    user.userName = opts['--username']
    user.userAlias = opts['--userAlias']
    job.user = user

    buildInfo = serialobj.BuildInfo()
    buildInfo.buildInfoName = opts['--buildInfoName']
    buildInfo.buildNumber = opts['--buildNumber']
    buildInfo.branch = opts['--branch']
    job.buildInfo = buildInfo

    jobType = serialobj.JobType()
    jobType.jobTypeName = opts['--jobTypeName']
    job.jobType = jobType

    jobGroup = serialobj.JobGroup()
    jobGroup.jobGroupName = opts['--jobGroupName']
    job.jobGroup = jobGroup

    job.jobDescription = opts['--jobDescription']
    job.jobName = opts['--jobName']

    root = serialobj.Root()
    root.job = job
    jsonOutput = serialobj.JsonOutput()
    jsonOutput.roots.append(root)
    with open(opts['--jsonFile'], 'w+') as opfile:
        opfile.write(jsonOutput.to_JSON())
        opfile.flush()
        opfile.close()

    log.info('Conversion of csv to json successful')

def run_json_conversion(opts):
    try:
        csvdict = read_csv(opts['--csvFile'])
        generate_json(opts, csvdict)
    except:
        return -1

    return 0

def main(args=None):
    def _main(settings, optlist, _):
        """
            Usage::
                csv_to_json.py
                    --csvFile csvFile to convert to json "MyLoc\\csvfile.csv"
                    --jsonFile output json file location "MyLoc\\jsonfile.json"
                    --jobName "sample job"
                    --jobDescription sample job description
                    --configName "sample config"
                    --jobGroupName sample job group
                    --jobTypeName "Private"
                    --username "sample user"
                    --userAlias "sampleuser"
                    --branch "ProjectK"
                    --buildInfoName "sample build"
                    --buildNumber "1234"
                    --machinepoolName "HP Z210 Workstation"
                    --machinepoolDescription "Intel64 Family 6 Model 42 Stepping 7"
                    --architectureName "AMD64"
                    --manufacturerName "Intel"
                    --microarchName "SSE2"
                    --numberOfCores "4"
                    --numberOfLogicalProcessors "8"
                    --totalPhysicalMemory "16342"
                    --osInfoName "Microsoft Windows 8.1 Pro"
                    --osVersion "6.3.9600"
                    --machineName "PCNAME"
                    --machineDescription "Intel(R) Core(TM) i7-2600 CPU @ 3.40GHz"


        """
        opts = dict(optlist)
        return run_json_conversion(opts)

    return command_main(_main, ['csvFile=', 'jsonFile=', 'jobName=', 'jobDescription=', 'configName=', 'jobGroupName=', 'jobTypeName=', 'username=', 'userAlias=', 'branch=', 'buildInfoName=', 'buildNumber=', 'machinepoolName=', 'machinepoolDescription=', 'architectureName=', 'manufacturerName=' , 'microarchName=', 'numberOfCores=', 'numberOfLogicalProcessors=', 'totalPhysicalMemory=', 'osInfoName=', 'osVersion=', 'machineName=', 'machineDescription='], args)

if __name__ == '__main__':
    sys.exit(main())