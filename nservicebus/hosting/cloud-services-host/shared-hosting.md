---
title: Shared Hosting in Azure
summary: Using Azure Cloud Services to host multiple NServiceBus endpoints on a shared pool of machines.
tags:
- Azure
- Cloud
- Hosting
- Worker Roles
related:
 - samples/azure/shared-host
redirects:
 - nservicebus/shared-hosting-nservicebus-in-windows-azure-cloud-services
 - nservicebus/azure/shared-hosting-nservicebus-in-windows-azure-cloud-services
---

If real scale is needed, as in tens, hundreds or even thousands of machines hosting each endpoint, than cloud services is the suitable deployment model. But very often, one only wants this scale when the project is eventually successfull, not when just starting out. To support this scenario, the `Host` endpoint role for Azure Cloud Services has been created.

This role allows to co-locate multiple endpoints on the same set of machines, while preserving the regular worker role programming model so that one can easily put each endpoint on it's own role again when required later.


## How it works

**Prerequisites** This approach assumes the endpoints are already [hosted in worker roles](/nservicebus/hosting/cloud-services-host/). The rest of this article will focus on how to transition from a multi worker environment to a shared hosting environment.

Instead of having endpoints packaged & deployed by the Azure infrastructure they are packaged as zip files and placed in a well known location (in Azure blob storage).

Then add a new worker role to the cloud services solution that will act as the host. This host will be configured to pull the endpoints from the well known location, extract them to disk and run them.


## Preparing the endpoint

Assuming the working endpoint is hosted in a worker role. Open the cloud services project, expand `Roles` and click remove on the worker role that is being prepared.

NOTE: Visual Studio will remove any configuration setting from the Azure configuration settings file. If any configuration overrides previously existed, effect the way the endpoint behaves, ensure those overrides are moved to the app.config file first or apply the alternative override system for shared hosts. See `Configuration concerns` further down this article for more details on this approach.

The role entry point also doubles as a host process for the endpoint, one that is aware of the service runtime and role context. This functionality needs to be replaced by another process in order to run the endpoint in a similar context as it would have when it was a separate role. This replacement host process is available on NuGet as the `NServiceBus.Hosting.Azure.HostProcess` package, install it in the worker role project.

Notice that an `NServiceBus.Hosting.Azure.HostProcess.exe` is now referenced. This exe can also run on a development machine outside the context of a service runtime. It can also be used to debug an endpoint locally without starting the Azure emulator. This is done by adding this exe to the debug path in the project properties.

Next pack the build output as a zip file so that the `NServiceBus.Hosting.Azure.HostProcess.exe` is in the root of the archive. (Just zip the debug or release directory)

Finally go to Azure storage account and create a private container called `endpoints` and put the zip file in there. Configure the host role entry point to download endpoints from this container later.


## Creating the host

Once prepared and uploaded all endpoints, add a new worker role project to the solution. This worker role will serve as a host for all the endpoints.

In this worker role one needs to reference the assembly that contains the Azure role entry point integration. The recommended way of doing this is by adding a NuGet package reference to the `NServiceBus.Hosting.Azure` package to the project.

To integrate the NServiceBus dynamic host into the worker role entry point, all one needs to do is to create a new instance of `NServiceBusRoleEntrypoint` and call it's `Start` and `Stop` methods in the appropriate `RoleEntryPoint` override.

Snippet:HostingInWorkerRole

Next to starting the role entry point, configure the endpoint behavior. In this case a hosting behavior, so that it will not run an endpoint itself but instead host other endpoints. To do so just specify the `AsA_Host` role for version 6 and below, or `IConfigureThisHost` for version 7 and above.

Snippet:AsAHost

The host entry point does require some configuration, it is necessary to tell it in what storage account to look for endpoints and how often it should do so, furthermore Azure needs to be configured to provision some space on the local disk, where the host can put the downloaded and extracted endpoints.

For version 7 and above, return this information from the `Configure` method in the class implementing the `IConfigureThisHost` interface.

For version 6 and below, add the following configuration settings entries to the `.csdef` file

Snippet:DynamicHostControllerConfig

For all versions a local storage resource must be configured, usually with the name `endpoints`. This will be the location to which the role entry point downloads the zip files of the endpoint packages.

Snippet:LocalResource

Other configuration settings are available as well if one needs a more fine grained control on how the host works:

 * `ConnectionString`: The connection string to the storage account containing the endpoint packages.
 * `Container`: The container where the endpoint packages are stored in the storage account, defaults to `endpoints`
 * `AutoUpdate`: Turn auto update on or off, defaults to `true`. Note that if it is set to `false`, then the host needs a reboot to pick new endpoints or versions of endpoints.
 * `UpdateInterval`: The time between checks if updates are available, in milliseconds, defaults to `600000`.
 * `LocalResource`: The name of the local storage resource where the zip archives will be extracted, defaults to `endpoints`
 * `TimeToWaitUntilProcessIsKilled`: When updating an endpoint to a new version, the host will kill the current process. Sometimes this fails or takes a very long time. This property specifies how long the host should wait, if this time elapses without the process going down, the host will reboot the machine (by throwing an exception). Default value: `10000`.
 * `RecycleRoleOnError`: By default Azure role instances will reboot when an exception is thrown from the role entrypoint, but not when thrown from a child process. If the role instance should reboot in this case as well, then set the `RecycleRoleOnError` on true. Then the host will start monitoring the child process for errors and request a recycle when it throws.


## Configuration concerns

The Azure configuration system applies to all instances of all roles. It has a built in way to separate role types, but not role instance and definitely no separation for processes on those instances. This means that a configuration override put in the service configuration file will automatically apply to all endpoints hosted on those roles. This is obviously not desirable, and can be dealt with in 2 ways.

 * Put the configuration settings in the app.config. As autoupdate is available one can easily manage it this way as changing a configuration means uploading a new zip to the Azure storage account and the hosts will update themselves automatically. (This is the default)
 * Alternatively one can separate the configuration settings in the service configuration file by convention. The `.AzureConfigurationSource(prefix)` overload allows to set a prefix in every endpoint that will be prepended to it's configuration settings. Call this configuration method with a prefix of choice and use the configuration settings file for the hosted endpoints.
