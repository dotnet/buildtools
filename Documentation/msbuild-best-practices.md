# MSBuild Best Practices
## Overview

Rules and guidelines for performant, stable, and maintainable MSBuild files.

Formatting and coding style information can be found in [msbuild-coding-style.md](msbuild-coding-style.md).

### Targets

- Document dependent properties and item groups.
- Document output properties and item groups.
- For targets that modify files support incremental builds using inputs and outputs.
    - Consider setting a property in target to let dependent targets know if the target has actually run.

### Item Groups

- Generate large item groups within targets (more than a few items).
    - Improves build performance.
    - Makes diagnostic builds easier to analyze.
    - Particularly important for recursive wildcard includes.
- Use Pascal casing for item names.

### Target Dependencies

- Define target dependencies against the lowest level targets possible.
- Use `DependsOnTargets` to describe workflow and provide injection points.
- Add conditions if you do not want to do work if a dependency is skipped (because it is up to date).

* * *

## MSBuild Gotchas

#### Doing work when a dependent target is skipped

Take the following target that wants to modify an compiled binary:

``` xml
<Target Name="MakeLargeAddressAware" AfterTargets="CoreCompile" BeforeTargets="Link">
```

While this seems reasonable at first glance, `CoreCompile` will be skipped if the inputs and outputs are up to date.
If the inputs haven't changed, the compile won't happen, but `MakeLargeAddressAware` will still be executed.
While you can add the same inputs and outputs for this target its complicated and risky to do so. Prefer to rely on a property
or item that you can know that [work has definitively been done](https://msdn.microsoft.com/en-us/library/ee264087.aspx#Anchor_1)- or create your own flag file to know that you're up to date.
In this particular case, the target we depend on invokes a set of targets if it actually does any work:

``` xml
  <PropertyGroup>
    <TargetsTriggeredByCompilation>
      MakeLargeAddressAware;$(TargetsTriggeredByCompilation)
    </TargetsTriggeredByCompilation>
  </PropertyGroup>
  <Target Name="MakeLargeAddressAware">
```


