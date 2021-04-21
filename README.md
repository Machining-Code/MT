# MT

A simple MTConnect client for the command-line. Running with no command-line arguments will start the client in interactive mode.

## Basic Usage

Commands are case-insensitive, but are shown in UPPERCASE in this document to distinguish commands from other arguments.
On the command-line, one or more commands may be entered consecutively.

- Get the device information from an agent:
  > mt CONNECT agent.mtconnect.org PROBE

- Get current status of a device named GFAgie01:
  > mt CONNECT agent.mtconnect.org CURRENT -deviceName GFAgie01

- Display list of commands:
  > mt HELP
  
- Display current status of all Condition dataItems, in JSON format:
  > mt CONNECT agent.mtconnect.org OPTION format json CURRENT -category Condition

- Display samples of type Angle
  > mt CONNECT agent.mtconnect.org OPTION format xml SAMPLE -type Angle

 ## Interactive Mode

  When running in interactive mode, one command may be entered on each line:

  > CONNECT agent.mtconnect.org
  
  > OPTION format xml
  
  > SAMPLE -type Angle

## Filtering

For CURRENT and SAMPLE commands, dataItems may be filtered by -id, -name, -type, -subType, and/or -category. 
If multiple filters are applied, the resulting output is a logical AND of all filters.
All filters are case-insensitive.

- Display current status of all Condition dataItems
  > CURRENT -category Condition

- Display all samples of type Availability
  > SAMPLE -type Availability

If the filter is wrapped in slashes, it is interpreted as a regular expression and matched accordingly.

- Display current status of all dataItems whose name starts with "coolant"
  > CURRENT -name /^coolant/

A generic filter may be applied to the dataItems, consisting of one or more key=value pairs separated by semicolons.
A key of "Tag" will match against the XML tag of the dataItem.
A key of "Value" will match against its value.
Any other key will be interpreted as an XML attribute.

- Display all "Availability" dataItems whose value is "UNAVAILABLE", and whose timestamp starts with "2021-04-21".
  > CURRENT -filter Tag=Availability;Value=UNAVAILABLE;timetsamp=/^2021-04-21*/

## Device Status

A simple device status monitor is built into the MT client.
This monitor will display each device configured in the MTConnect agent,
along with a tree showing all components of the device, and color-coded condition.

  > mt CONNECT agent.mtconnect.org STATUS

