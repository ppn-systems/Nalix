# Struture 0.1.72

```text
C:.
│   Notio.Build.props
│   Notio.sln
│
├───Notio.Application
│   │   Notio.Application.csproj
│   │
│   ├───RestApi
│   │       AuthController.cs
│   │       MainController.cs
│   │       MessageController.cs
│   │
│   └───Threading
│           AppConfig.cs
│           Program.cs
│
├───Notio.Common
│   │   InternalErrorException.cs
│   │   Notio.Common.csproj
│   │   SelfCheck.cs
│   │
│   ├───Connection
│   │       IConnectEventArgs.cs
│   │       IConnection.cs
│   │
│   ├───Enums
│   │       ConnectionError.cs
│   │       ConnectionState.cs
│   │       LoggingLevel.cs
│   │
│   ├───Exceptions
│   │       PackageException.cs
│   │       StringConversionException.cs
│   │
│   ├───Firewall
│   │       IRateLimiter.cs
│   │
│   ├───Logging
│   │       EventId.cs
│   │       ILogger.cs
│   │       ILoggingFormatter.cs
│   │       ILoggingPublisher.cs
│   │       ILoggingTarget.cs
│   │       NotioDebug.cs
│   │
│   ├───Memory
│   │       IBufferPool.cs
│   │       IPoolable.cs
│   │
│   └───Models
│           Authoritys.cs
│           LoggingEntry.cs
│
├───Notio.Cryptography
│   │   Aes256.cs
│   │   Arc4.cs
│   │   Notio.Cryptography.csproj
│   │   Rsa4096.cs
│   │   Srp6.cs
│   │   Xtea.cs
│   │
│   ├───Hash
│   │       SaltedHashPassword.cs
│   │
│   └───Mode
│           AesCbcMode.cs
│           AesCtrMode.cs
│           AesGcmMode.cs
│
├───Notio.Database
│   │   appsettings.json
│   │   Notio.Database.csproj
│   │   NotioContext.cs
│   │   NotioContextFactory.cs
│   │
│   ├───Configurations
│   │       BaseEntityConfiguration.cs
│   │       ChatConfiguration.cs
│   │       MessageAttachmentConfiguration.cs
│   │       MessageConfiguration.cs
│   │       UserChatConfiguration.cs
│   │       UserConfiguration.cs
│   │
│   ├───Entities
│   │       BaseEntity.cs
│   │       Chat.cs
│   │       Message.cs
│   │       MessageAttachment.cs
│   │       User.cs
│   │       UserChat.cs
│   │
│   ├───Enums
│   │       ChatType.cs
│   │       MessageType.cs
│   │       UserRole.cs
│   │
│   ├───Extensions
│   │       DbContextExtensions.Chat.cs
│   │       DbContextExtensions.cs
│   │       DbContextExtensions.Group.cs
│   │       DbContextExtensions.Message.cs
│   │       DbContextExtensions.MessageAttachment.cs
│   │       DbContextExtensions.User.cs
│   │
│   └───Migrations
│           20250130054223_InitialCreate.cs
│           20250130054223_InitialCreate.Designer.cs
│           20250130062059_AddIsDeletedColumn.cs
│           20250130062059_AddIsDeletedColumn.Designer.cs
│           20250130062349_UpdatePasswordHashColumn.cs
│           20250130062349_UpdatePasswordHashColumn.Designer.cs
│           NotioContextModelSnapshot.cs
│
├───Notio.Lite
│   │   CompositeHashCode.cs
│   │   Definitions.cs
│   │   Definitions.Types.cs
│   │   FromString.cs
│   │   Notio.Lite.csproj
│   │   ObjectComparer.cs
│   │   OperatingSystem.cs
│   │   Paginator.cs
│   │   Runtime.cs
│   │   StructEndiannessAttribute.cs
│   │
│   ├───Collections
│   │       CollectionCacheRepository.cs
│   │
│   ├───Diagnostics
│   │       Benchmark.cs
│   │       BenchmarkUnit.cs
│   │       HighResolutionTimer.cs
│   │
│   ├───Extensions
│   │       ByteArrayExtensions.cs
│   │       ExceptionExtensions.cs
│   │       Extensions.cs
│   │       Extensions.Dictionaries.cs
│   │       FunctionalExtensions.cs
│   │       IEnumerableExtensions.cs
│   │       PropertyProxyExtensions.cs
│   │       ReflectionExtensions.cs
│   │       StringExtensions.cs
│   │       TaskExtensions.cs
│   │       ValueTypeExtensions.cs
│   │
│   ├───Formatters
│   │       CsvReader.cs
│   │       CsvWriter.cs
│   │       HumanizeJson.cs
│   │       Json.Converter.cs
│   │       Json.cs
│   │       Json.Deserializer.cs
│   │       Json.Serializer.cs
│   │       Json.SerializerOptions.cs
│   │       JsonPropertyAttribute.cs
│   │       JsonSerializerCase.cs
│   │
│   ├───Mappers
│   │       CopyableAttribute.cs
│   │       IObjectMap.cs
│   │       ObjectMap.cs
│   │       ObjectMapper.cs
│   │       ObjectMapper.PropertyInfoComparer.cs
│   │
│   ├───Net
│   │   │   IPAddressRange.cs
│   │   │   IPAddressRangeExtensions.cs
│   │   │   IPAddressUtility.cs
│   │   │
│   │   └───Internal
│   │           IPAddressValue.cs
│   │
│   ├───Parsers
│   │       Operator.cs
│   │       Token.cs
│   │       Tokenizer.cs
│   │       TokenType.cs
│   │       VerbOptionAttribute.cs
│   │
│   ├───Reflection
│   │       AttributeCache.cs
│   │       ConstructorTypeCache.cs
│   │       ExtendedTypeInfo.cs
│   │       IPropertyProxy.cs
│   │       MethodInfoCache.cs
│   │       PropertyInfoProxy.cs
│   │       PropertyTypeCache.cs
│   │       TypeCache.cs
│   │
│   └───Threading
│           PeriodicTask.cs
│
├───Notio.Logging
│   │   Notio.Logging.csproj
│   │   NotioLog.cs
│   │   NotioLogConfig.cs
│   │
│   ├───Engine
│   │       LoggingEngine.cs
│   │       LoggingPublisher.cs
│   │
│   ├───Format
│   │       ColorAnsi.cs
│   │       LoggingBuilder.cs
│   │       LoggingFormatter.cs
│   │       LoggingLevelFormatter.cs
│   │
│   ├───Storage
│   │       FileError.cs
│   │       FileLoggerOptions.cs
│   │       FileLoggerProvider.cs
│   │       FileWriter.cs
│   │
│   └───Targets
│           ConsoleLoggingTarget.cs
│           FileLoggingTarget.cs
│
├───Notio.Network
│   │   FirewallConfig.cs
│   │   NetworkConfig.cs
│   │   Notio.Network.csproj
│   │
│   ├───Connection
│   │   │   Connection.cs
│   │   │   ConnectionEventArgs.cs
│   │   │
│   │   └───Args
│   ├───Firewall
│   │   │   BandwidthLimiter.cs
│   │   │   ConnectionLimiter.cs
│   │   │   RequestLimiter.cs
│   │   │
│   │   ├───Configuration
│   │   │       BandwidthConfig.cs
│   │   │       ConnectionConfig.cs
│   │   │       RateLimitConfig.cs
│   │   │
│   │   ├───Enums
│   │   │       BandwidthLimit.cs
│   │   │       ConnectionLimit.cs
│   │   │       RequestLimit.cs
│   │   │
│   │   └───Models
│   │           BandwidthInfo.cs
│   │           ConnectionInfo.cs
│   │           RateLimitInfo.cs
│   │           RequestDataInfo.cs
│   │
│   ├───Handlers
│   │       PacketCommandAttribute.cs
│   │       PacketController.cs
│   │       PacketHandlerRouter.cs
│   │
│   ├───Listeners
│   │       IListener.cs
│   │       Listener.cs
│   │       LoginListener.cs
│   │
│   └───Protocols
│           IProtocol.cs
│           LoginProtocol.cs
│           Protocol.cs
│
├───Notio.Package
│   │   Notio.Package.csproj
│   │   Packet.cs
│   │
│   ├───Enums
│   │       PacketFlags.cs
│   │       PacketPriority.cs
│   │       PacketType.cs
│   │
│   ├───Extensions
│   │       HashCodeExtensions.cs
│   │       PackageExtensions.Compress.cs
│   │       PackageExtensions.Crypto.cs
│   │       PackageExtensions.cs
│   │       PackageExtensions.Pool.cs
│   │       PackageExtensions.Signature.cs
│   │
│   ├───Helpers
│   │       PacketFlagsHelper.cs
│   │       PacketJsonHelper.cs
│   │       PacketPriorityHelper.cs
│   │       PacketTypeHelper.cs
│   │
│   ├───Models
│   │       PacketOffset.cs
│   │       PacketSize.cs
│   │
│   ├───Serialization
│   │       PacketSerializer.cs
│   │       PacketSerializerUnsafe.cs
│   │
│   └───Utilities
│           PacketOperations.cs
│           PacketVerifier.cs
│
├───Notio.Shared
│   │   DefaultDirectories.cs
│   │   Notio.Shared.csproj
│   │   Singleton.cs
│   │   SingletonBase.cs
│   │
│   ├───Configuration
│   │       ConfigurationBinder.cs
│   │       ConfigurationException.cs
│   │       ConfigurationIgnoreAttribute.cs
│   │       ConfigurationIniFile.cs
│   │       ConfigurationShared.cs
│   │       ConfigureObject.cs
│   │
│   ├───Extensions
│   │       ByteArrayComparer.cs
│   │       EnumExtensions.cs
│   │       StreamExtentions.cs
│   │
│   ├───Helper
│   │       AssemblyHelper.cs
│   │
│   ├───Identification
│   │       GenId.cs
│   │       TypeId.cs
│   │       UniqueId.cs
│   │
│   ├───Management
│   │       InfoCPU.cs
│   │       InfoMemory.cs
│   │       InfoOS.cs
│   │       System32Cmd.cs
│   │
│   ├───Memory
│   │   ├───Buffer
│   │   │       BufferAllocator.cs
│   │   │       BufferConfig.cs
│   │   │       BufferInfo.cs
│   │   │       BufferManager.cs
│   │   │       BufferPoolShared.cs
│   │   │
│   │   ├───Cache
│   │   │       BinaryCache.cs
│   │   │       FifoCache.cs
│   │   │       LRUCache.cs
│   │   │
│   │   └───Pool
│   │           ListPool.cs
│   │           ObjectPool.cs
│   │           ObjectPoolManager.cs
│   │           PooledMemory.cs
│   │
│   ├───Random
│   │       GRandom.cs
│   │       Rand.cs
│   │       RandMwc.cs
│   │
│   └───Time
│           Clock.cs
│
├───Notio.Storage
│   │   IFileStorage.cs
│   │   IFileStorageAsync.cs
│   │   IStreamProvider.cs
│   │   Notio.Storage.csproj
│   │
│   ├───Config
│   │       IFileStorageConfig.cs
│   │       InDiskConfig.cs
│   │       InMemoryConfig.cs
│   │
│   ├───FileFormats
│   │       IFileFormat.cs
│   │       Original.cs
│   │
│   ├───Generator
│   │       FileGenerateResponse.cs
│   │       FileGenerator.cs
│   │       IFileGenerator.cs
│   │
│   ├───Helper
│   │       UrlExpiration.cs
│   │
│   ├───Local
│   │       InDiskStorage.cs
│   │       InMemoryStorage.cs
│   │
│   ├───MimeTypes
│   │       IMimeTypeResolver.cs
│   │       MimeTypeMapper.cs
│   │       MimeTypePattern.cs
│   │       MimeTypeResolver.cs
│   │
│   └───Models
│           FileMeta.cs
│           IFile.cs
│           LocalFile.cs
│
├───Notio.Testing
│       Aes256Testing.cs
│       Notio.Testing.csproj
│       PacketTesting.cs
│
└───Notio.Web
    │   ExceptionHandler.cs
    │   ICookieCollection.cs
    │   IWebServer.cs
    │   LICENSE
    │   ModuleGroup.cs
    │   Notio.Web.csproj
    │   WebServer-Constants.cs
    │   WebServer.cs
    │   WebServerBase`1.cs
    │   WebServerExtensions-ExceptionHandliers.cs
    │   WebServerExtensions-SessionManager.cs
    │   WebServerExtensions.cs
    │   WebServerOptions.cs
    │   WebServerOptionsBase.cs
    │   WebServerOptionsBaseExtensions.cs
    │   WebServerOptionsExtensions.cs
    │   WebServerStateChangedEventArgs.cs
    │   WebServerStateChangedEventHandler.cs
    │
    ├───Actions
    │       ActionModule.cs
    │       RedirectModule.cs
    │
    ├───Authentication
    │       Auth.cs
    │       BasicAuthenticationModule.cs
    │       BasicAuthenticationModuleBase.cs
    │       BasicAuthenticationModuleExtensions.cs
    │
    ├───Cors
    │       CorsModule.cs
    │
    ├───Enums
    │       CloseStatusCode.cs
    │       CompressionMethod.cs
    │       HttpListenerMode.cs
    │       HttpVerbs.cs
    │       Opcode.cs
    │       WebServerState.cs
    │
    ├───Files
    │   │   DirectoryLister.cs
    │   │   FileCache.cs
    │   │   FileCache.Section.cs
    │   │   FileModule.cs
    │   │   FileModuleExtensions.cs
    │   │   FileRequestHandler.cs
    │   │   FileRequestHandlerCallback.cs
    │   │   FileSystemProvider.cs
    │   │   IDirectoryLister.cs
    │   │   IFileProvider.cs
    │   │   MappedResourceInfo.cs
    │   │   ResourceFileProvider.cs
    │   │   ZipFileProvider.cs
    │   │
    │   └───Internal
    │           Base64Utility.cs
    │           EntityTag.cs
    │           FileCacheItem.cs
    │           HtmlDirectoryLister.cs
    │           MappedResourceInfoExtensions.cs
    │
    ├───Http
    │       ExceptionHandlerCallback.cs
    │       HttpContextExtensions-Items.cs
    │       HttpContextExtensions-Redirect.cs
    │       HttpContextExtensions-Requests.cs
    │       HttpContextExtensions-RequestStream.cs
    │       HttpContextExtensions-Responses.cs
    │       HttpContextExtensions-ResponseStream.cs
    │       HttpContextExtensions.cs
    │       HttpException-Shortcuts.cs
    │       HttpException.cs
    │       HttpExceptionHandler.cs
    │       HttpExceptionHandlerCallback.cs
    │       HttpHeaderNames.cs
    │       HttpNotAcceptableException.cs
    │       HttpRangeNotSatisfiableException.cs
    │       HttpRedirectException.cs
    │       HttpStatusDescription.cs
    │       IHttpContext.cs
    │       IHttpContextHandler.cs
    │       IHttpContextImpl.cs
    │       IHttpException.cs
    │       IHttpListener.cs
    │       IHttpMessage.cs
    │       IHttpRequest.cs
    │       IHttpResponse.cs
    │
    ├───Internal
    │       BufferingResponseStream.cs
    │       CompressionStream.cs
    │       CompressionUtility.cs
    │       DummyWebModuleContainer.cs
    │       LockableNameValueCollection.cs
    │       MimeTypeCustomizer.cs
    │       RequestHandlerPassThroughException.cs
    │       TimeKeeper.cs
    │       UriUtility.cs
    │       WebModuleCollection.cs
    │
    ├───MimeTypes
    │       IMimeTypeCustomizer.cs
    │       IMimeTypeProvider.cs
    │       MimeType.Associations.cs
    │       MimeType.cs
    │       MimeTypeCustomizerExtensions.cs
    │
    ├───Net
    │   │   CookieList.cs
    │   │   EndPointManager.cs
    │   │   HttpListener.cs
    │   │
    │   └───Internal
    │           EndPointListener.cs
    │           HeaderUtility.cs
    │           HttpConnection.cs
    │           HttpConnection.InputState.cs
    │           HttpConnection.LineState.cs
    │           HttpListenerContext.cs
    │           HttpListenerPrefixCollection.cs
    │           HttpListenerRequest.cs
    │           HttpListenerResponse.cs
    │           HttpListenerResponseHelper.cs
    │           ListenerPrefix.cs
    │           NetExtensions.cs
    │           RequestStream.cs
    │           ResponseStream.cs
    │           StringExtensions.cs
    │           SystemCookieCollection.cs
    │           SystemHttpContext.cs
    │           SystemHttpListener.cs
    │           SystemHttpRequest.cs
    │           SystemHttpResponse.cs
    │           WebSocketHandshakeResponse.cs
    │
    ├───Request
    │       HttpRequestExtensions.cs
    │       RequestDeserializer.cs
    │       RequestDeserializerCallback`1.cs
    │       RequestHandler.cs
    │       RequestHandlerCallback.cs
    │
    ├───Response
    │       HttpResponseExtensions.cs
    │       ResponseSerializer.cs
    │       ResponseSerializerCallback.cs
    │
    ├───Routing
    │       BaseRouteAttribute.cs
    │       Route.cs
    │       RouteAttribute.cs
    │       RouteHandlerCallback.cs
    │       RouteMatch.cs
    │       RouteMatcher.cs
    │       RouteResolutionResult.cs
    │       RouteResolverBase`1.cs
    │       RouteResolverCollectionBase`2.cs
    │       RouteVerbResolver.cs
    │       RouteVerbResolverCollection.cs
    │       RoutingModule.cs
    │       RoutingModuleBase.cs
    │       RoutingModuleExtensions-AddHandlerFromBaseOrTerminalRoute.cs
    │       RoutingModuleExtensions-AddHandlerFromRouteMatcher.cs
    │       RoutingModuleExtensions-AddHandlerFromTerminalRoute.cs
    │       RoutingModuleExtensions.cs
    │       SyncRouteHandlerCallback.cs
    │
    ├───Security
    │   │   BanInfo.cs
    │   │   IIPBanningCriterion.cs
    │   │   IPBanningConfiguration.cs
    │   │   IPBanningModule.cs
    │   │   IPBanningModuleExtensions.cs
    │   │   IPBanningRegexCriterion.cs
    │   │   IPBanningRequestsCriterion.cs
    │   │   JwtToken.cs
    │   │
    │   └───Internal
    │           IPBanningExecutor.cs
    │
    ├───Sessions
    │   │   ISession.cs
    │   │   ISessionManager.cs
    │   │   ISessionProxy.cs
    │   │   LocalSessionManager.cs
    │   │   LocalSessionManager.SessionImpl.cs
    │   │   Session.cs
    │   │   SessionExtensions.cs
    │   │   SessionProxy.cs
    │   │
    │   └───Internal
    │           DummySessionProxy.cs
    │
    ├───Utilities
    │       ComponentCollectionExtensions.cs
    │       ComponentCollection`1.cs
    │       CompressionMethodNames.cs
    │       DisposableComponentCollection`1.cs
    │       HttpDate.cs
    │       IComponentCollection`1.cs
    │       IPParser.cs
    │       MimeTypeProviderStack.cs
    │       NameValueCollectionExtensions.cs
    │       QValueList.cs
    │       QValueListExtensions.cs
    │       StringExtensions.cs
    │       UniqueIdGenerator.cs
    │       UrlEncodedDataParser.cs
    │       UrlPath.cs
    │       Validate-MimeType.cs
    │       Validate-Paths.cs
    │       Validate-Rfc2616.cs
    │       Validate-Route.cs
    │       Validate.cs
    │
    ├───WebApi
    │       FormDataAttribute.cs
    │       FormFieldAttribute.cs
    │       IRequestDataAttribute`1.cs
    │       IRequestDataAttribute`2.cs
    │       JsonDataAttribute.cs
    │       QueryDataAttribute.cs
    │       QueryFieldAttribute.cs
    │       WebApiController.cs
    │       WebApiModule.cs
    │       WebApiModuleBase.cs
    │       WebApiModuleExtensions.cs
    │
    ├───WebModule
    │       IWebModule.cs
    │       IWebModuleContainer.cs
    │       WebModuleBase.cs
    │       WebModuleContainer.cs
    │       WebModuleContainerExtensions-Actions.cs
    │       WebModuleContainerExtensions-Cors.cs
    │       WebModuleContainerExtensions-Files.cs
    │       WebModuleContainerExtensions-Routing.cs
    │       WebModuleContainerExtensions-Security.cs
    │       WebModuleContainerExtensions-WebApi.cs
    │       WebModuleContainerExtensions.cs
    │       WebModuleExtensions-ExceptionHandlers.cs
    │       WebModuleExtensions.cs
    │
    └───WebSockets
        │   IWebSocket.cs
        │   IWebSocketContext.cs
        │   IWebSocketReceiveResult.cs
        │   WebSocketException.cs
        │   WebSocketModule.cs
        │
        └───Internal
                Fin.cs
                FragmentBuffer.cs
                Mask.cs
                MessageEventArgs.cs
                PayloadData.cs
                Rsv.cs
                StreamExtensions.cs
                SystemWebSocket.cs
                SystemWebSocketReceiveResult.cs
                WebSocket.cs
                WebSocketContext.cs
                WebSocketFrame.cs
                WebSocketFrameStream.cs
                WebSocketReceiveResult.cs
                WebSocketStream.cs
```
