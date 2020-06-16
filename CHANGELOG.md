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