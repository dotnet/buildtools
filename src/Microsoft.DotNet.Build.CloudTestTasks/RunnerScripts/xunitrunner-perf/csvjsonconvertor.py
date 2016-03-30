import csv
import getopt
import helix.proc
import json
import os
import os.path
import serialobj
import sys
sys.path.append(os.getenv('HELIX_SCRIPT_ROOT'))
import helix.logs
from helix.cmdline import command_main

log = helix.logs.get_logger()

# read a row of csv data into dict
def add_row(test, value, csvdict):
    """
        test names are of the form
        System.ComponentModel.Tests.Perf_TypeDescriptorTests.GetConverter(typeToConvert: typeof(bool), expectedConverter: typeof(System.ComponentModel.BooleanConverter))
        where the string before the first '(' are the identifiers for the test and the string after is function metadata
        strip off the metadata when generating the namespace/function identifiers and combine them when again for function identifier
        eg.: System.ComponentModel.Tests.Perf_TypeDescriptorTests.GetConverter(typeToConvert: typeof(bool), expectedConverter: typeof(System.ComponentModel.BooleanConverter))
        will be converted to following identifiers
        [System, ComponentModel, Tests, Perf_TypeDescriptorTests, GetConverter(typeToConvert: typeof(bool), expectedConverter: typeof(System.ComponentModel.BooleanConverter))]
    """

    test = test.strip('\"')
    funcMeta = test.split('(')
    funcMeta = funcMeta[1:]
    test = test.split('(')[0]
    identifiers = test.split('.')
    funcName = identifiers[-1]
    for data in funcMeta:
        funcName = funcName + '(' + data

    identifiers[-1] = funcName
    currdict = csvdict
    for identifier in identifiers[:-1]:
        if identifier not in currdict:
            currdict[identifier] = dict()

        currdict = currdict[identifier]

    if identifiers[-1] not in currdict:
        currdict[identifiers[-1]] = list()

    currdict[identifiers[-1]].append(value)

# read csv data into dict
def read_csv(csvFile):
    log.info('Reading '+csvFile)
    if not os.path.exists(csvFile):
        raise Exception(csvFile+' could not be found for conversion')

    csvdict = dict()
    with open(csvFile, 'rb') as csvfile:
        reader = csv.reader(csvfile)
        for row in reader:
            add_row(row[2], row[3], csvdict)

    return csvdict

# generate result object with raw values
def generate_result_object(resultvalues):
    result = serialobj.Result()
    # generate measurement result test object per iteration
    measurements = list()
    measurement = serialobj.Measurement()
    measurement.iterValues = resultvalues
    measurement.measurementType = 'execution_time'
    measurements.append(measurement)
    result.measurements = measurements
    return result

# generate a test object per node; each node represents a namespace/function (recursively)
def generate_test_object(opts, currdict, testName):
    currTest = serialobj.Test()
    currTest.testName = testName

    for key, value in currdict.iteritems():
        test = serialobj.Test()
        if isinstance(value, dict):
            test = generate_test_object(opts, value, key)
        elif isinstance(value, list):
            test.testName = key
            test.results.append(generate_result_object(value))
            test.machine.machineName = opts['--machineName']
            test.machine.machineDescription = opts['--machineDescription']

        currTest.tests.append(test)

    return currTest


# build json using csv data and meta data
def generate_json(opts, csvdict):
    log.info('Attempting to generate '+opts['--jsonFile'])
    rootTests = list()

    # recursively build nodes from the csvdict
    rootTest = generate_test_object(opts, csvdict, opts['--branch']+' Perf Test Results')
    rootTests.append(rootTest)

    # populate the root level meta info
    run = serialobj.Run()
    run.testList = rootTests

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
    except Exception as ex:
        log.error(ex.args)
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