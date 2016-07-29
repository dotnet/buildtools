"""
 This is a temporary solution to gather h/w info from machine
 the package is installed on end machines running perf
 However this runs on a limited number of platforms
 (but supports all the flavors we currently use for testing)
 moving forward we will need a truly cross-plat solution here
"""
from cpuinfo import cpuinfo
import csv
import getopt
import helix.proc
import json
import os
import os.path
import platform
import psutil
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
def generate_result_object(resultvalues, metric):
    result = serialobj.Result()
    # generate measurement result test object per iteration
    measurements = list()
    measurement = serialobj.Measurement()
    measurement.iterValues = resultvalues
    measurement.measurementType = metric
    if metric:
        measurements.append(measurement)
    result.measurements = measurements
    return result

# generate a test object per node; each node represents a namespace/function (recursively)
def generate_test_object(currdict, testName, info, metric):
    currTest = serialobj.Test()
    currTest.testName = testName

    for key, value in currdict.iteritems():
        test = serialobj.Test()
        if isinstance(value, dict):
            test = generate_test_object(value, key, info, metric)
        elif isinstance(value, list):
            test.testName = key
            test.results.append(generate_result_object(value, metric))
            test.machine.machineName = platform.node()
            test.machine.machineDescription = format(info['brand'])

        currTest.tests.append(test)

    return currTest


# build json using csv data and meta data
def generate_json(opts, csvdicts):
    perfsettingsjson = ''
    with open(os.path.join(opts['--perfSettingsJson'])) as perfsettingsjson:
        # read the perf-specific settings
        perfsettingsjson = json.loads(perfsettingsjson.read())

    jsonFilePath = opts['--jsonFile']
    log.info('Attempting to generate '+jsonFilePath)
    rootTests = list()

    info = cpuinfo.get_cpu_info()

    for metric, currdict in csvdicts.iteritems():
        # recursively build nodes from the csvdict
        rootTest = generate_test_object(currdict, perfsettingsjson['TestProduct']+' Perf Test Results', info, metric)
        rootTests.append(rootTest)

    # populate the root level meta info
    run = serialobj.Run()
    run.testList = rootTests

    machinepool = serialobj.MachinePool()

    architecture = serialobj.Architecture()
    architecture.architectureName = format(info['arch'])
    machinepool.architecture = architecture

    manufacturer = serialobj.Manufacturer()
    manufacturer.manufacturerName = format(info['vendor_id'])
    machinepool.manufacturer = manufacturer

    microarch = serialobj.MicroArch()
    microarch.microarchName = 'SSE2' # cannot be obtained by cpu-info; need to figure out some other way

    osInfo = serialobj.OSInfo()
    osInfo.osInfoName = platform.system()
    osInfo.osVersion = platform.version()

    machinepool.osInfo = osInfo
    machinepool.microarch = microarch
    machinepool.NumberOfCores = psutil.cpu_count(logical=False)
    machinepool.NumberOfLogicalProcessors = psutil.cpu_count(logical=True)
    machinepool.TotalPhysicalMemory = psutil.virtual_memory().total/1024
    machinepool.machinepoolName = perfsettingsjson['TargetQueue']
    machinepool.machinepoolDescription = '...'
    run.machinepool = machinepool

    config = serialobj.Config()
    config.configName = perfsettingsjson['TargetQueue']
    run.config = config


    runs = list()
    runs.append(run)

    job = serialobj.Job()
    job.Runs = runs

    user = serialobj.User()
    user.userName = perfsettingsjson['Creator']
    user.userAlias = perfsettingsjson['Creator']
    job.user = user

    # extract build number from buildmoniker if official build
    buildtokens = perfsettingsjson['BuildMoniker'].split('-')
    if len(buildtokens) < 3:
        buildNumber = perfsettingsjson['BuildMoniker']
    else:
        buildNumber = buildtokens[-2] +'.'+buildtokens[-1]


    buildInfo = serialobj.BuildInfo()
    buildInfo.buildInfoName = perfsettingsjson['BuildMoniker']
    buildInfo.buildNumber = buildNumber
    buildInfo.branch = perfsettingsjson['TestProduct']
    job.buildInfo = buildInfo

    jobType = serialobj.JobType()
    jobType.jobTypeName = 'Private'
    job.jobType = jobType

    jobGroup = serialobj.JobGroup()
    jobGroup.jobGroupName = perfsettingsjson['Creator']+'-'+perfsettingsjson['TestProduct']+'-'+perfsettingsjson['Branch']+'-Perf'
    job.jobGroup = jobGroup

    job.jobDescription = '...'
    job.jobName = opts['--jobName']

    root = serialobj.Root()
    root.job = job
    jsonOutput = serialobj.JsonOutput()
    jsonOutput.roots.append(root)

    with open(jsonFilePath, 'w+') as opfile:
        opfile.write(jsonOutput.to_JSON())
        opfile.flush()
        opfile.close()

    log.info('Conversion of csv to json successful')


def run_json_conversion(opts):
    csvdir = opts['--csvDir']
    csvfiles = []
    for item in os.listdir(csvdir):
        if item.lower().endswith('.csv'):
            csvfiles.append(os.path.join(csvdir, item))

    if not csvfiles:
        log.error('no csv files found for conversion under '+csvdir)
        return -1

    try:
        csvdicts = dict()
        for item in csvfiles:
            csvdicts[item.replace(csvdir, '').replace('.csv', '')] = read_csv(os.path.join(csvdir, item))

        generate_json(opts, csvdicts)
    except Exception as ex:
        log.error(ex.args)
        return -1

    return 0

def main(args=None):
    def _main(settings, optlist, _):
        """
            Usage::
                csv_to_json.py
                    --csvDir dir where csvs can be found
                    --jsonFile json file path
                    --jobName "sample job"
                    --perfSettingsJson json file containing perf-specific settings
        """


        opts = dict(optlist)
        return run_json_conversion(opts)

    return command_main(_main, ['csvDir=', 'jsonFile=', 'jobName=', 'perfSettingsJson='], args)

if __name__ == '__main__':
    sys.exit(main())