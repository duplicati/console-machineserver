# Machine Service Project

## Overview

This project is the machine service application. It handles requests and validates authentication via a message bus. Statistics are written to a database.

## Environment Variables

The following environment variables are required for the project to start:

| Variable                            | Description                                                           |
| ----------------------------------- | --------------------------------------------------------------------- |
| ENVIRONMENT\_\_ISPROD               | Production environment flag (false indicates development environment) |
| ENVIRONMENT\_\_MACHINESERVERPRIVATE | Base64 encoded RSA private key for machine server authentication      |
| DATABASE\_\_CONNECTIONSTRING        | PostgreSQL connection string for connecting to database for stats     |
| MESSAGING\_\_CONNECTIONSTRING       | PostgreSQL connection string for connecting to the message bus        |

When running in a Debug setting, the database and messaging connection strings are optional, and a new key is generated automatically.

### Optional environment variables

The following environment variables are optional, and should be considered for a production deployment:

| Variable                                    | Description                                                                      |
| ------------------------------------------- | -------------------------------------------------------------------------------- |
| DATABASE\_\_ADMINCONNECTIONSTRING           | Connectionstring for updating the schema                                         |
| ENVIRONMENT\_\_MACHINENAME                  | Name of the machine for logging purposes                                         |
| ENVIRONMENT\_\_INSTANCEID                   | Unique id for identifying the instance, auto assigned if not set                 |
| ENVIRONMENT\_\_REDIRECTURL                  | Url to redirect to when visiting the root path                                   |
| ENVIRONMENT\_\_VERIFYSCHEMA                 | Flag toggling if schema validation is performed                                  |
| ENVIRONMENT\_\_GIT_COMMIT_VERSION           | The Git commit hash for logging (set in Docker image)                            |
| ENVIRONMENT\_\_MAXMESSAGESIZE               | Maximum number of bytes allowed for a single authenticated message               |
| ENVIRONMENT\_\_MAXBYTESBEFOREAUTHENTICATION | Maximum number of bytes allowed on an unauthenticated connection                 |
| ENVIRONMENT\_\_WEBSOCKETRECEIVEBUFFERSIZE   | The size of the receive buffer for websocket messages, in bytes                  |
| ENVIRONMENT\_\_SECONDSBETWEENSTATISTICS     | The number of seconds between writing machine service statistics to the database |
| ENVIRONMENT\_\_STATUSREPORTINTERVALSECONDS  | The number of seconds between logged status reports                              |
| ENVIRONMENT\_\_PRECOMPILEDDBCLASSES         | Set this if the compilation has already complied the database classes            |
| ENVIRONMENT\_\_GATEWAYPRESHAREDKEY          | A pre-shared key used to authenticating gateways and servers                     |
| ENVIRONMENT\_\_LICENSEKEY                   | A license key; required if using any proprietary features                        |
| ENVIRONMENT\_\_STATISTICSRETENTIONDAYS      | Number of days to retain statistics in the database                              |
| CORS\_\_ALLOWEDORIGINS                      | Semicolon-separated list of allowed CORS origins                                 |
| IPBLACKLIST\_\_STORAGE                      | The KVPSButter connection string to the storage that contains an IP blacklist    |
| IPBLACKLIST\_\_ENTRY                        | The key that contains the IP blacklist                                           |
| SECURITY\_\_MAXREQUESTSPERSECONDPERIP       | The maximum number of request from a single IP per second before throttling it   |
| SECURITY\_\_FILTERPATTERNS                  | Boolean toggling filtering of scanning patterns                                  |
| SECURITY\_\_RATELIMITENABLED                | Boolean toggling if IP rate limiting is enabled                                  |

## Logging Configuration

| Variable                                    | Description                             |
| ------------------------------------------- | --------------------------------------- |
| SERILOG\_\_MINIMUMLEVEL\_\_DEFAULT          | Default minimum log level for Serilog   |
| SERILOG\_\_SOURCETOKEN                      | The token used to log data from serilog |
| SERILOG\_\_ENDPOINT                         | The serilog endpoint to send logs to    |
| LOGGING\_\_LOGLEVEL\_\_DEFAULT              | Default logging level                   |
| LOGGING\_\_LOGLEVEL\_\_MICROSOFT_ASPNETCORE | ASP.NET Core specific logging level     |
