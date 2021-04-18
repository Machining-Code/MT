# MT

A simple MTConnect client for the command-line. Running with no command-line arguments will start the client in interactive mode.

Sample usage:

Commands are case-insensitive, but are shown in UPPERCASE here to distinguish them from arguments.

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

For CURRENT and SAMPLE commands, dataItems may be filtered by -id, -name, -type, -subType, and/or -category. 
If multiple filters are applied, the resulting output is a logical AND of all filters.

When running in interactive mode, one command may be entered on each line:

  > CONNECT agent.mtconnect.org
  > OPTION format xml
  > SAMPLE -type Angle