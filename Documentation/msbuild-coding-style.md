# MSBuild Coding Style
## Overview

The guidelines are based on common formatting found in the shipped MSBuild target files. See the [sample.targets](sample.targets) file for a well-formatted example.

Additional best practices can be found in [msbuild-best-practices.md](msbuild-best-practices.md).

## Basic Formatting

### Spacing
- Use spaces for indenting.
- Indents should be two spaces.
- Nested tags should be indented.
- Avoid trailing whitespace and whitespace only lines.
- Closing tag final character(s) should not be on a line by themselves (`/>`, `>`).
- Single space before closing tag an empty element (`<Tag />` `<Tag></Tag>`).
- Single space around operators in conditions.

### Casing
- Use Pascal-case for item and property names.
- Use lower case `"true"` and `"false"` in conditions.
- Use lower case `and` and `or` in conditions.

### Atributes
- Prefer breaking multiple attributes into multiple lines.
- Left-align attributes on the attribute name when broken into multiple lines.
- Use double quotes for attributes, single quotes within.
- Favor ordering as documented in the [Schema Reference](https://msdn.microsoft.com/en-us/library/ms164283.aspx).

### Comments
- Put start and end tags on separate lines for multiple line comments and indent text.
- Use a consistent header to label Targets and important comments.
- Keep right hand margin for comments consistent, around 80 or so characters.

### Items and Properties
- Property and item names should be Pascal-cased.
- "Lists" should have semicolon terminated items on separate lines.
- Keep large item groups inside targets (for performance and debugging clarity).

``` xml
<PropertyGroup>
  <FooDependsOn>
    Alpha;
    Beta;
  </FooDependsOn>
  <Bar>true</Bar>
</PropertyGroup>
```

### Conditions
- Use lower case `"true"` and `"false"` in conditions.
- Use lower case `and` and `or` in conditions.
- Single quote properties and constants.
- Use parenthesis for clarity where needed.

### Document
- Files should have an xml declaration that sets the version as `"1.0"` and the encoding as `"utf-8"`.
- The `<Project>` tag should include a ToolsVersion of `"14.0"` and the msbuild namespace.

``` xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />
</Project>
```

## Sample Target

``` xml
  <!--
    ===============================================================================
      {Sample}
    ===============================================================================

    This is a sample target and header. By using a consistent header, searching for
    a given target is easier. Documenting what the target does and its inputs and
    outputs are critical for understanding item and property dependencies.

    Trivial targets do not require a header. Consider discoverability for targets
    when deciding to create a header.

    Use the same number of characters for the header "lines", don't try and match
    the length of the target name. Enclosing the name in brackets makes searching
    files easier.

      [In]
      @(Foo) - Foo items to turn into Bar

      [Out]
      @(Bar) - Foo items turned into Bar
  -->
  <Target Name="Sample"
          Condition="'$(SkipSample)' != 'false' and 'true' == 'true'">
    <Message Text="Running the sample target" />
    <ItemGroup>
      <Bar Include="@(Foo)" />
    </ItemGroup>
  </Target>
```
