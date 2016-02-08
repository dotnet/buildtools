import json
import copy
class Measurement:
    def __init__(self):
        self.measurementType = None
        self.measurementValue = None
        self.greaterTheBetter = None
        self.iterValues = list()
        self.stdDeviation = None
        self.additionalInfo = None

class Result:
    def __init__(self):
        self.phaseName = None
        self.phaseDescription = None
        self.measurements = list()
        self.results = list()

class Test:
    def __init__(self):
        self.testName = None
        self.testDescription = None
        self.testPath = None
        self.testExternalReference = None
        self.inputLoc = None
        self.outputLoc = None
        self.setupRun = None
        self.bugId = None
        self.analyzed = None
        self.additionalOptions = None
        self.machine = Machine()
        self.hostMachine = Machine()
        self.status = None
        self.tests = list()
        self.results = list()


class Machine:
    def __init__(self):
        self.machineName = None
        self.machineDescription = None

class Config:
    def __init__(self):
        self.configName = None
        self.configOptions = None
        self.compilerswitches = None
        self.linkerswitches = None

class Architecture:
    def __init__(self):
        self.architectureName = None
        self.architectureDescription = None

class Manufacturer:
    def __init__(self):
        self.manufacturerName = None
        self.manufacturerDescription = None

class MicroArch:
    def __init__(self):
        self.microarchName = None
        self.microarchDescription = None

class OSInfo:
    def __init__(self):
        self.osInfoName = None
        self.osVersion = None
        self.osEdition = None

class MachinePool:
    def __init__(self):
        self.machinepoolName = None
        self.machinepoolDescription = None
        self.architecture = Architecture()
        self.manufacturer = Manufacturer()
        self.microarch = MicroArch()
        self.NumberOfCores = None
        self.NumberOfLogicalProcessors = None
        self.TotalPhysicalMemory = None
        self.osInfo = OSInfo()

class Run:
    def __init__(self):
        self.externalDependency = None
        self.config = Config()
        self.machinepool = MachinePool()
        self.hostMachinepool = MachinePool()
        self.testList = list()


class JobGroup:
    def __init__(self):
        self.jobGroupName = None
        self.jobGroupDescription = None

class JobType:
    def __init__(self):
        self.jobTypeName = None
        self.jobTypeDescription = None

class BuildInfo:
    def __init__(self):
        self.buildInfoName = None
        self.branch = None
        self.buildNumber = None
        self.shelveset = None
        self.version = None
        self.flavor = None
        self.changeset = None

class User:
    def __init__(self):
        self.userName = None
        self.userAlias = None

class Job:
    def __init__(self):
        self.jobName = None
        self.baselineJobName = None
        self.jobDescription = None
        self.createTime = None
        self.externalDependency = None
        self.jobGroup = JobGroup()
        self.jobType = JobType()
        self.buildInfo = BuildInfo()
        self.user = User()
        self.Runs = list()
        self.optimizations = None

class Root:
    def __init__(self):
        self.job = Job()

class JsonOutput:
    def __init__(self):
        self.roots = list()

    def __defaultjson(self, o):
        objdict = copy.deepcopy(o.__dict__)
        delkeys = list()
        for key in objdict:
            if key.startswith('__'):
                delkeys.append(key)

        for key in delkeys:
            del objdict[key]

        return objdict

    def to_JSON(self):
        return json.dumps(self, default=self.__defaultjson, sort_keys=True, indent=4)
