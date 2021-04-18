# MT

A simple MTConnect client for the command-line. Running with no command-line arguments will start the client in interactive mode.

Sample usage:

Commands are case-insensitive, but are shown in UPPERCASE here to distinguish them from arguments.

- Get the device information from an agent:
  > mt CONNECT http://agent.mtconnect.org PROBE

- Get current status of a device named GFAgie01:
  > mt CONNECT http://agent.mtconnect.org CURRENT -deviceName GFAgie01

- Display list of commands:
  > mt HELP
  
