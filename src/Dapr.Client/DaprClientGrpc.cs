﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Http;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Autogenerated = Autogen.Grpc.v1;

    /// <summary>
    /// A client for interacting with the Dapr endpoints.
    /// </summary>
    internal class DaprClientGrpc : DaprClient
    {
         private static readonly string daprErrorInfoHTTPCodeMetadata  = "http.code";
        private static readonly string daprErrorInfoHTTPErrorMetadata = "http.error_message";
        private static readonly string grpcStatusDetails = "grpc-status-details-bin";
        private static readonly string grpcErrorInfoDetail = "google.rpc.ErrorInfo";
        private static readonly string daprHTTPStatusHeader = "dapr-http-status";

        private readonly Autogenerated.Dapr.DaprClient client;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        // property exposed for testing purposes
        internal Autogenerated.Dapr.DaprClient Client => client;

        // property exposed for testing purposes
        internal JsonSerializerOptions JsonSerializerOptions => jsonSerializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClientGrpc"/> class.
        /// </summary>
        /// <param name="channel">gRPC channel to create gRPC clients.</param>
        /// <param name="jsonSerializerOptions">Json serialization options.</param>
        internal DaprClientGrpc(GrpcChannel channel, JsonSerializerOptions jsonSerializerOptions = null)
        {
            this.jsonSerializerOptions = jsonSerializerOptions;
            this.client = new Autogenerated.Dapr.DaprClient(channel);
        }

        #region Publish Apis
        /// <inheritdoc/>
        public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
            ArgumentVerifier.ThrowIfNull(data, nameof(data));
            return MakePublishRequest(pubsubName, topicName, data, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task PublishEventAsync(string pubsubName, string topicName, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
            return MakePublishRequest(pubsubName, topicName, string.Empty, cancellationToken);
        }

        private async Task MakePublishRequest<TContent>(string pubsubName, string topicName, TContent content, CancellationToken cancellationToken)
        {
            // Create PublishEventEnvelope
            var envelope = new Autogenerated.PublishEventRequest()
            {
                PubsubName = pubsubName,
                Topic = topicName,
            };

            if (content != null)
            {
                envelope.Data = TypeConverters.ToJsonByteString(content, this.jsonSerializerOptions);
            }

            await this.MakeGrpcCallHandleError(
                options => client.PublishEventAsync(envelope, options),
                cancellationToken);
        }
        #endregion

        #region InvokeBinding Apis

        /// <inheritdoc/>
        public override async Task InvokeBindingAsync<TRequest>(
            string name,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentVerifier.ThrowIfNullOrEmpty(operation, nameof(operation));

            _ = await MakeInvokeBindingRequestAsync(name, operation, data, metadata, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<TResponse> InvokeBindingAsync<TRequest, TResponse>(
            string name,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentVerifier.ThrowIfNullOrEmpty(operation, nameof(operation));

            Autogenerated.InvokeBindingResponse response = await MakeInvokeBindingRequestAsync(name, operation, data, metadata, cancellationToken);
            return ConvertFromInvokeBindingResponse<TResponse>(response, this.jsonSerializerOptions);
        }

        private static T ConvertFromInvokeBindingResponse<T>(Autogenerated.InvokeBindingResponse response, JsonSerializerOptions options = null)
        {
            var responseData = response.Data.ToStringUtf8();
            return JsonSerializer.Deserialize<T>(responseData, options);
        }

        private async Task<Autogenerated.InvokeBindingResponse> MakeInvokeBindingRequestAsync<TContent>(
           string name,
           string operation,
           TContent data,
           Dictionary<string, string> metadata = default,
           CancellationToken cancellationToken = default)
        {
            var envelope = new Autogenerated.InvokeBindingRequest()
            {
                Name = name,
                Operation = operation
            };

            if (data != null)
            {
                envelope.Data = TypeConverters.ToJsonByteString(data, this.jsonSerializerOptions);
            }

            if (metadata != null)
            {
                envelope.Metadata.Add(metadata);
            }

            return await this.MakeGrpcCallHandleError(
                options => client.InvokeBindingAsync(envelope, options),
                cancellationToken);
        }
        #endregion

        #region InvokeMethod Apis
        public override async Task InvokeMethodAsync(
           string appId,
           string methodName,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            _ = await this.MakeInvokeRequestAsync(appId, methodName, null, httpExtension, cancellationToken);
        }

        public override async Task InvokeMethodAsync<TRequest>(
           string appId,
           string methodName,
           TRequest data,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            Any serializedData = null;
            if (data != null)
            {
                serializedData = TypeConverters.ToAny(data, this.jsonSerializerOptions);
            }

            _ = await this.MakeInvokeRequestAsync(appId, methodName, serializedData, httpExtension, cancellationToken);
        }

        public override async ValueTask<TResponse> InvokeMethodAsync<TResponse>(
           string appId,
           string methodName,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            var response = await this.MakeInvokeRequestAsync(appId, methodName, null, httpExtension, cancellationToken);
            return response.Data.Value.IsEmpty ? default : TypeConverters.FromAny<TResponse>(response.Data, this.jsonSerializerOptions);
        }

        public override async ValueTask<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            HTTPExtension httpExtension = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            var request = new ServiceInvocationRequest<TRequest>
            {
                AppId = appId,
                MethodName = methodName,
                Body = data,
            };

            // Any serializedData = null;
            // if (data != null)
            // {
            //     serializedData = TypeConverters.ToAny(data, this.jsonSerializerOptions);
            // }

            var invokeResponse = await this.MakeInvokeRequestAsyncWithResponse<TRequest, TResponse>(request, httpExtension, cancellationToken);
            return invokeResponse.Body;
        }

        public override async Task<ServiceInvocationResponse<TRequest, TResponse>> InvokeMethodWithResponseHeadersAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            Dapr.Client.Http.HTTPExtension httpExtension = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNull(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNull(methodName, nameof(methodName));

            

            var request = new ServiceInvocationRequest<TRequest>
            {
                AppId = appId,
                MethodName = methodName,
                Body = data,
            };

            // Any serializedData = null;
            // if (request.Body != null)
            // {
            //     serializedData = TypeConverters.ToAny(request.Body, this.jsonSerializerOptions);
            // }

            var invokeResponse = new ServiceInvocationResponse<TRequest, TResponse>(request);
            // try
            // {
                invokeResponse = await this.MakeInvokeRequestAsyncWithResponse<TRequest, TResponse>(request, request.HttpExtension, cancellationToken);
                
            // }
            // catch (RpcException ex)
            // {
            //     var entry = ex.Trailers.Get(grpcStatusDetails);
            //     if (entry != null)
            //     {
            //         var status = Google.Rpc.Status.Parser.ParseFrom(entry.ValueBytes);
            //         foreach(var detail in status.Details)
            //         {
            //             if(Google.Protobuf.WellKnownTypes.Any.GetTypeName(detail.TypeUrl) == grpcErrorInfoDetail)
            //             {
            //                 var rpcError = detail.Unpack<Google.Rpc.ErrorInfo>();
            //                 invokeResponse.GrpcStatusInfo.InnerHttpStatusCode = Convert.ToInt32(rpcError.Metadata[daprErrorInfoHTTPCodeMetadata]);
            //                 invokeResponse.GrpcStatusInfo.InnerHttpErrorMessage = rpcError.Metadata[daprErrorInfoHTTPErrorMetadata];
            //             }
            //         }
            //     }
                
            //     throw new ServiceInvocationException<TRequest, TResponse>($"Exception while invoking {request.MethodName} on appId:{request.AppId}", ex, invokeResponse);
            // }

            return invokeResponse;
        }

        public override async ValueTask<TResponse> InvokeMethodRawAsync<TResponse>(
           string appId,
           string methodName,
           byte[] data,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            var serializedData = TypeConverters.ToAny(data, this.jsonSerializerOptions);
            var response = await this.MakeInvokeRequestAsync(appId, methodName, serializedData, httpExtension, cancellationToken);
            return response.Data.Value.IsEmpty ? default : TypeConverters.FromAny<TResponse>(response.Data, this.jsonSerializerOptions);
        }

        public override async ValueTask<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            if (keys.Count == 0)
                throw new ArgumentException("keys do not contain any elements");

            var getBulkStateEnvelope = new Autogenerated.GetBulkStateRequest()
            {
                StoreName = storeName,
                Parallelism = parallelism ?? default(int)
            };

            getBulkStateEnvelope.Keys.AddRange(keys);

            var response = await this.MakeGrpcCallHandleError(
                options => client.GetBulkStateAsync(getBulkStateEnvelope, options),
                cancellationToken);

            var bulkResponse = new List<BulkStateItem>();

            foreach (var item in response.Items)
            {
                bulkResponse.Add(new BulkStateItem(item.Key, item.Data.ToStringUtf8(), item.Etag));
            }

            return bulkResponse;
        }

        private AsyncUnaryCall<Autogenerated.InvokeResponse> MakeInvokeRequestAsync(
            string appId,
            string methodName,
            Any data,
            HTTPExtension httpExtension,
            CancellationToken cancellationToken = default)
        {
            var protoHTTPExtension = new Autogenerated.HTTPExtension();
            var contentType = "";
            Metadata headers = null;

            if (httpExtension != null)
            {
                protoHTTPExtension.Verb = ConvertHTTPVerb(httpExtension.Verb);

                if (httpExtension.QueryString != null)
                {
                    foreach (var (key, value) in httpExtension.QueryString)
                    {
                        protoHTTPExtension.Querystring.Add(key, value);
                    }
                }

                if (httpExtension.Headers != null)
                {
                    headers = new Metadata();
                    foreach (var (key, value) in httpExtension.Headers)
                    {
                        headers.Add(key, value);
                    }
                }

                contentType = httpExtension.ContentType ?? Constants.ContentTypeApplicationJson;
            }
            else
            {
                protoHTTPExtension.Verb = Autogenerated.HTTPExtension.Types.Verb.Post;
                contentType = Constants.ContentTypeApplicationJson;
            }

            var invokeRequest = new Autogenerated.InvokeRequest()
            {
                Method = methodName,
                Data = data,
                ContentType = contentType,
                HttpExtension = protoHTTPExtension
            };

            var request = new Autogenerated.InvokeServiceRequest()
            {
                Id = appId,
                Message = invokeRequest,
            };

            var callOptions = new CallOptions(headers: headers ?? new Metadata(), cancellationToken: cancellationToken);

            // add token for dapr api token based authentication
            var daprApiToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");

            if (daprApiToken != null)
            {
                callOptions.Headers.Add("dapr-api-token", daprApiToken);
            }

            // Common Exception Handling logic can be added here for all calls.
            var grpcCall = client.InvokeServiceAsync(request, callOptions);
            return grpcCall;
        }

        private async Task<ServiceInvocationResponse<TRequest, TResponse>> MakeInvokeRequestAsyncWithResponse<TRequest, TResponse>(
            ServiceInvocationRequest<TRequest> request,
            HTTPExtension httpExtension,
            CancellationToken cancellationToken = default)
        {
            
            var invokeResponse = new ServiceInvocationResponse<TRequest, TResponse>(request);
            Any serializedData = null;
            if (request.Body != null)
            {
                serializedData = TypeConverters.ToAny(request.Body, this.jsonSerializerOptions);
            }

            try
            {
                var grpcCall = MakeInvokeRequestAsync(request.AppId, request.MethodName, serializedData, httpExtension, cancellationToken);

                var response = await grpcCall.ResponseAsync;
                var responseHeaders = await grpcCall.ResponseHeadersAsync;
                var trailers = grpcCall.GetTrailers();
                var grpcStatus = grpcCall.GetStatus();

                var headers = grpcCall.ResponseHeadersAsync.Result.ToDictionary(kv => kv.Key, kv => kv.ValueBytes);

                    invokeResponse.Body = response.Data.Value.IsEmpty ? default : TypeConverters.FromAny<TResponse>(response.Data, this.jsonSerializerOptions);
                    invokeResponse.Headers = headers;
                    invokeResponse.Trailers = grpcCall.GetTrailers().ToDictionary(kv => kv.Key, kv => kv.ValueBytes);

                if (IsResponseFromHttpCallee(headers))
                {
                    invokeResponse.HttpStatusCode = (HttpStatusCode)System.Enum.Parse(typeof(HttpStatusCode), Encoding.UTF8.GetString(headers[daprHTTPStatusHeader], 0, headers[daprHTTPStatusHeader].Length));
                    invokeResponse.ContentType = Constants.ContentTypeApplicationJson;
                }
                else
                {
                    // Response is grpc
                    invokeResponse.ContentType = Constants.ContentTypeApplicationGrpc;
                    invokeResponse.GrpcStatusInfo = new GRPCStatusInfo(grpcStatus.StatusCode, grpcStatus.Detail);
                }
            }
            catch (RpcException ex)
            {
                var entry = ex.Trailers.Get(grpcStatusDetails);
                if (entry != null)
                {
                    var status = Google.Rpc.Status.Parser.ParseFrom(entry.ValueBytes);
                    foreach (var detail in status.Details)
                    {
                        if (Google.Protobuf.WellKnownTypes.Any.GetTypeName(detail.TypeUrl) == grpcErrorInfoDetail)
                        {
                            var rpcError = detail.Unpack<Google.Rpc.ErrorInfo>();
                            invokeResponse.GrpcStatusInfo.InnerHttpStatusCode = Convert.ToInt32(rpcError.Metadata[daprErrorInfoHTTPCodeMetadata]);
                            invokeResponse.GrpcStatusInfo.InnerHttpErrorMessage = rpcError.Metadata[daprErrorInfoHTTPErrorMetadata];
                        }
                    }
                }

                throw new ServiceInvocationException<TRequest, TResponse>($"Exception while invoking {request.MethodName} on appId:{request.AppId}", ex, invokeResponse);
            }

            return invokeResponse;
        }

        private bool IsResponseFromHttpCallee(Dictionary<string,byte[]> headers)
        {
            foreach(var header in headers)
            {
                if(header.Key == daprHTTPStatusHeader)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region State Apis
        /// <inheritdoc/>
        public override async ValueTask<TValue> GetStateAsync<TValue>(
            string storeName,
            string key,
            ConsistencyMode? consistencyMode = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var getStateEnvelope = new Autogenerated.GetStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (metadata != null)
            {
                getStateEnvelope.Metadata.Add(metadata);
            }

            if (consistencyMode != null)
            {
                getStateEnvelope.Consistency = GetStateConsistencyForConsistencyMode(consistencyMode.Value);
            }

            var response = await this.MakeGrpcCallHandleError(
                options => client.GetStateAsync(getStateEnvelope, options),
                cancellationToken);

            if (response.Data.IsEmpty)
            {
                return default;
            }

            var responseData = response.Data.ToStringUtf8();
            return JsonSerializer.Deserialize<TValue>(responseData, this.jsonSerializerOptions);
        }

        /// <inheritdoc/>
        public override async ValueTask<(TValue value, string etag)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var getStateEnvelope = new Autogenerated.GetStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (consistencyMode != null)
            {
                getStateEnvelope.Consistency = GetStateConsistencyForConsistencyMode(consistencyMode.Value);
            }

            var response = await this.MakeGrpcCallHandleError(
                options => client.GetStateAsync(getStateEnvelope, options),
                cancellationToken);

            if (response.Data.IsEmpty)
            {
                return (default, response.Etag);
            }

            var responseData = response.Data.ToStringUtf8();
            var deserialized = JsonSerializer.Deserialize<TValue>(responseData, this.jsonSerializerOptions);
            return (deserialized, response.Etag);
        }

        /// <inheritdoc/>
        public override async Task SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            await this.MakeSaveStateCallAsync(
                storeName,
                key,
                value,
                etag: null,
                stateOptions,
                metadata,
                cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<bool> TrySaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            try
            {
                await this.MakeSaveStateCallAsync(storeName, key, value, etag, stateOptions, metadata, cancellationToken);
                return true;
            }
            catch (RpcException)
            {
            }

            return false;
        }

        private async ValueTask MakeSaveStateCallAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag = default,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            // Create PublishEventEnvelope
            var saveStateEnvelope = new Autogenerated.SaveStateRequest()
            {
                StoreName = storeName,
            };

            var stateItem = new Autogenerated.StateItem()
            {
                Key = key,
            };

            if (metadata != null)
            {
                stateItem.Metadata.Add(metadata);
            }

            if (etag != null)
            {
                stateItem.Etag = etag;
            }

            if (stateOptions != null)
            {
                stateItem.Options = ToAutoGeneratedStateOptions(stateOptions);
            }

            if (value != null)
            {
                stateItem.Value = TypeConverters.ToJsonByteString(value, this.jsonSerializerOptions);
            }

            saveStateEnvelope.States.Add(stateItem);

            await this.MakeGrpcCallHandleError(
                options => client.SaveStateAsync(saveStateEnvelope, options),
                cancellationToken);
        }


        /// <inheritdoc/>
        public override async Task ExecuteStateTransactionAsync(
            string storeName,
            IReadOnlyList<StateTransactionRequest> operations,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNull(operations, nameof(operations));
            if (operations.Count == 0)
            {
                throw new ArgumentException($"{nameof(operations)} does not contain any elements");
            }

            await this.MakeExecuteStateTransactionCallAsync(
                storeName,
                operations,
                metadata,
                cancellationToken);
        }

        private async ValueTask MakeExecuteStateTransactionCallAsync(
            string storeName,
            IReadOnlyList<StateTransactionRequest> states,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            var executeStateTransactionRequestEnvelope = new Autogenerated.ExecuteStateTransactionRequest()
            {
                StoreName = storeName,
            };

            foreach (var state in states)
            {
                var stateOperation = new Autogenerated.TransactionalStateOperation();

                stateOperation.OperationType = state.OperationType.ToString().ToLower();
                stateOperation.Request = ToAutogeneratedStateItem(state);

                executeStateTransactionRequestEnvelope.Operations.Add(stateOperation);

            }

            // Add metadata that applies to all operations if specified
            if (metadata != null)
            {
                executeStateTransactionRequestEnvelope.Metadata.Add(metadata);
            }

            await this.MakeGrpcCallHandleError(
                options => client.ExecuteStateTransactionAsync(executeStateTransactionRequestEnvelope, options),
                cancellationToken);
        }

        private Autogenerated.StateItem ToAutogeneratedStateItem(StateTransactionRequest state)
        {
            var stateOperation = new Autogenerated.StateItem();
            stateOperation.Key = state.Key;

            if (state.Value != null)
            {
                stateOperation.Value = ByteString.CopyFrom(state.Value);
            }

            if (state.ETag != null)
            {
                stateOperation.Etag = state.ETag;
            }

            if (state.Metadata != null)
            {
                stateOperation.Metadata.Add(state.Metadata);
            }

            if (state.Options != null)
            {
                stateOperation.Options = ToAutoGeneratedStateOptions(state.Options);
            }

            return stateOperation;
        }


        /// <inheritdoc/>
        public override async Task DeleteStateAsync(
            string storeName,
            string key,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            await this.MakeDeleteStateCallAsync(
                storeName,
                key,
                etag: null,
                stateOptions,
                metadata,
                cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<bool> TryDeleteStateAsync(
            string storeName,
            string key,
            string etag,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            try
            {
                await this.MakeDeleteStateCallAsync(storeName, key, etag, stateOptions, metadata, cancellationToken);
                return true;
            }
            catch (Exception)
            {
            }

            return false;
        }

        private async ValueTask MakeDeleteStateCallAsync(
           string storeName,
           string key,
           string etag = default,
           StateOptions stateOptions = default,
           Dictionary<string, string> metadata = default,
           CancellationToken cancellationToken = default)
        {
            var deleteStateEnvelope = new Autogenerated.DeleteStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (metadata != null)
            {
                deleteStateEnvelope.Metadata.Add(metadata);
            }

            if (etag != null)
            {
                deleteStateEnvelope.Etag = etag;
            }

            if (stateOptions != null)
            {
                deleteStateEnvelope.Options = ToAutoGeneratedStateOptions(stateOptions);
            }

            await this.MakeGrpcCallHandleError(
                options => client.DeleteStateAsync(deleteStateEnvelope, options),
                cancellationToken);
        }
        #endregion

        #region Secret Apis
        /// <inheritdoc/>
        public async override ValueTask<Dictionary<string, string>> GetSecretAsync(
            string storeName,
            string key,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var envelope = new Autogenerated.GetSecretRequest()
            {
                StoreName = storeName,
                Key = key
            };

            if (metadata != null)
            {
                envelope.Metadata.Add(metadata);
            }

            var response = await this.MakeGrpcCallHandleError(
                 options => client.GetSecretAsync(envelope, options),
                 cancellationToken);

            return response.Data.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Makes Grpc call using the cancellationToken and handles Errors.
        /// All common exception handling logic will reside here.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="callFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private Task<TResponse> MakeGrpcCallHandleError<TResponse>(Func<CallOptions, AsyncUnaryCall<TResponse>> callFunc, CancellationToken cancellationToken = default)
        {
            return MakeGrpcCallHandleError<TResponse>(callFunc, null, cancellationToken);
        }

        /// <summary>
        /// Makes Grpc call using the cancellationToken and handles Errors.
        /// All common exception handling logic will reside here.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="callFunc"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<TResponse> MakeGrpcCallHandleError<TResponse>(Func<CallOptions, AsyncUnaryCall<TResponse>> callFunc, Metadata headers, CancellationToken cancellationToken = default)
        {
            var callOptions = new CallOptions(headers: headers ?? new Metadata(), cancellationToken: cancellationToken);

            // add token for dapr api token based authentication
            var daprApiToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");

            if (daprApiToken != null)
            {
                callOptions.Headers.Add("dapr-api-token", daprApiToken);
            }

            // Common Exception Handling logic can be added here for all calls.
            return await callFunc.Invoke(callOptions);
        }

        private Autogenerated.StateOptions ToAutoGeneratedStateOptions(StateOptions stateOptions)
        {
            var stateRequestOptions = new Autogenerated.StateOptions();

            if (stateOptions.Consistency != null)
            {
                stateRequestOptions.Consistency = GetStateConsistencyForConsistencyMode(stateOptions.Consistency.Value);
            }

            if (stateOptions.Concurrency != null)
            {
                stateRequestOptions.Concurrency = GetStateConcurrencyForConcurrencyMode(stateOptions.Concurrency.Value);
            }

            return stateRequestOptions;
        }

        private static Autogenerated.HTTPExtension.Types.Verb ConvertHTTPVerb(HTTPVerb verb)
        {
            return verb switch
            {
                HTTPVerb.Get => Autogenerated.HTTPExtension.Types.Verb.Get,
                HTTPVerb.Head => Autogenerated.HTTPExtension.Types.Verb.Head,
                HTTPVerb.Post => Autogenerated.HTTPExtension.Types.Verb.Post,
                HTTPVerb.Put => Autogenerated.HTTPExtension.Types.Verb.Put,
                HTTPVerb.Delete => Autogenerated.HTTPExtension.Types.Verb.Delete,
                HTTPVerb.Connect => Autogenerated.HTTPExtension.Types.Verb.Connect,
                HTTPVerb.Options => Autogenerated.HTTPExtension.Types.Verb.Options,
                HTTPVerb.Trace => Autogenerated.HTTPExtension.Types.Verb.Trace,
                _ => throw new NotImplementedException($"Service invocation with verb '{verb}' is not supported")
            };
        }

        private static Autogenerated.StateOptions.Types.StateConsistency GetStateConsistencyForConsistencyMode(ConsistencyMode consistencyMode)
        {
            return consistencyMode switch
            {
                ConsistencyMode.Eventual => Autogenerated.StateOptions.Types.StateConsistency.ConsistencyEventual,
                ConsistencyMode.Strong => Autogenerated.StateOptions.Types.StateConsistency.ConsistencyStrong,
                _ => throw new ArgumentException($"{consistencyMode} Consistency Mode is not supported.")
            };
        }

        private static Autogenerated.StateOptions.Types.StateConcurrency GetStateConcurrencyForConcurrencyMode(ConcurrencyMode concurrencyMode)
        {
            return concurrencyMode switch
            {
                ConcurrencyMode.FirstWrite => Autogenerated.StateOptions.Types.StateConcurrency.ConcurrencyFirstWrite,
                ConcurrencyMode.LastWrite => Autogenerated.StateOptions.Types.StateConcurrency.ConcurrencyLastWrite,
                _ => throw new ArgumentException($"{concurrencyMode} Concurrency Mode is not supported.")
            };
        }

        private HttpStatusCode ToHttpStatusCode(Grpc.Core.StatusCode code)
        {
            switch (code)
            {
                case Grpc.Core.StatusCode.OK:
                    return HttpStatusCode.OK;
                case Grpc.Core.StatusCode.Cancelled:
                    return HttpStatusCode.RequestTimeout;
                case Grpc.Core.StatusCode.Unknown:
                    return HttpStatusCode.InternalServerError;
                case Grpc.Core.StatusCode.InvalidArgument:
                    return HttpStatusCode.BadRequest;
                case Grpc.Core.StatusCode.DeadlineExceeded:
                    return HttpStatusCode.GatewayTimeout;
                case Grpc.Core.StatusCode.NotFound:
                    return HttpStatusCode.NotFound;
                case Grpc.Core.StatusCode.AlreadyExists:
                    return HttpStatusCode.Conflict;
                case Grpc.Core.StatusCode.PermissionDenied:
                    return HttpStatusCode.Forbidden;
                case Grpc.Core.StatusCode.Unauthenticated:
                    return HttpStatusCode.Unauthorized;
                case Grpc.Core.StatusCode.ResourceExhausted:
                    return HttpStatusCode.TooManyRequests;
                case Grpc.Core.StatusCode.FailedPrecondition:
                    // Note, this deliberately doesn't translate to the similarly named '412 Precondition Failed' HTTP response status.
                    return HttpStatusCode.BadRequest;
                case Grpc.Core.StatusCode.Aborted:
                    return HttpStatusCode.Conflict;
                case Grpc.Core.StatusCode.OutOfRange:
                    return HttpStatusCode.BadRequest;
                case Grpc.Core.StatusCode.Unimplemented:
                    return HttpStatusCode.NotImplemented;
                case Grpc.Core.StatusCode.Internal:
                    return HttpStatusCode.InternalServerError;
                case Grpc.Core.StatusCode.Unavailable:
                    return HttpStatusCode.ServiceUnavailable;
                case Grpc.Core.StatusCode.DataLoss:
                    return HttpStatusCode.InternalServerError;
            }

            return HttpStatusCode.InternalServerError;
        }
        #endregion Helper Methods
    }
}
