## 0.1.24 (16-12-2024): 

Bump NuGet deps versions

## 0.1.23 (11-03-2024)

Added option "ServiceLocatorSettings.ObserveNonExistentApplications".

## 0.1.22 (07-02-2024)

Added method `SetHostnameProvider` for configuring hostname in `IReplicaInfoBuilder`

## 0.1.21 (31-08-2022):

Added missing `ExecutionContext.SuppressFlow` for long-running tasks.

## 0.1.20 (13-01-2022):

Reduced memory traffic and added several optimizations to the ServiceLocator.Locate method.

## 0.1.19 (11-01-2022):

Added service discovery events sending.

## 0.1.18 (06-12-2021):

Added `net6.0` target.

## 0.1.16 (12-11-2021):

Fixed bug with 'resurrecting' environment on explicit environment deletion.
Added option to create environment on beacon start if absent.

## 0.1.15 (21-09-2021):

Updated `EnvironmentInfo.Application` from `vostok.commons.environment` module.

## 0.1.14 (28-06-2021):

Add possibility to register replica tags on ServiceBeacon start.

## 0.1.12 (12-06-2021):

IReplicaInfoBuilder: allow to override replica identifier.

## 0.1.11 (05-04-2021):

ServiceBeacon no longer writes assembly commit hashes into ephemeral node data by default to ease load on ZooKeeper.

## 0.1.10 (08-12-2020):

Support `\r\n` properties delimiter.

## 0.1.9 (16-06-2020):

Use FQDN by default in ServiceBeaconSettings.

## 0.1.8 (06-04-2020):

Local hostname can be configured using 'VOSTOK_LOCAL_HOSTNAME' environment variables.

## 0.1.7 (15-02-2020):

https://github.com/vostok/servicediscovery/issues/3

## 0.1.6 (04-02-2020):

Process all ZooKeeper events via events queue, instead of `Task.Run(() => ...)`. 

## 0.1.5 (28-01-2019):

Added `TryCreateApplicationAsync` and `TryDeleteApplicationAsync` methods.

## 0.1.4 (05-11-2019):

Added `RegistrationAllowedProvider` to `ServiceBeacon`.

## 0.1.3 (12-09-2019):

Removed models for IServiceDiscoveryManager:
	ApplicationInfo,
	EnvironmentInfo,
	ReplicaInfo,
	ServiceTopology

## 0.1.2 (12-09-2019):

Implement IServiceDiscoveryManager.

## 0.1.1 (06-08-2019):

Provide a way to customize ZooKeeper path escaper via settings.

## 0.1.0 (15-05-2019): 

Initial prerelease.
