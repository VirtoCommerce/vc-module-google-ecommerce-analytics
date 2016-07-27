using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RestSharp;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Model;

namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Api
{
    /// <summary>
    /// Represents a collection of functions to interact with the API endpoints
    /// </summary>
    public interface IVirtoCommerceGoogleEcommerceAnalyticsApi : IApiAccessor
    {
        #region Synchronous Operations
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>StoreSettings</returns>
        StoreSettings StoreSettingsGetSettings(string storeId);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>ApiResponse of StoreSettings</returns>
        ApiResponse<StoreSettings> StoreSettingsGetSettingsWithHttpInfo(string storeId);
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns></returns>
        void StoreSettingsUpdate(StoreSettings settings, string storeId);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns>ApiResponse of Object(void)</returns>
        ApiResponse<Object> StoreSettingsUpdateWithHttpInfo(StoreSettings settings, string storeId);
        #endregion Synchronous Operations
        #region Asynchronous Operations
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>Task of StoreSettings</returns>
        System.Threading.Tasks.Task<StoreSettings> StoreSettingsGetSettingsAsync(string storeId);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>Task of ApiResponse (StoreSettings)</returns>
        System.Threading.Tasks.Task<ApiResponse<StoreSettings>> StoreSettingsGetSettingsAsyncWithHttpInfo(string storeId);
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns>Task of void</returns>
        System.Threading.Tasks.Task StoreSettingsUpdateAsync(StoreSettings settings, string storeId);

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns>Task of ApiResponse</returns>
        System.Threading.Tasks.Task<ApiResponse<object>> StoreSettingsUpdateAsyncWithHttpInfo(StoreSettings settings, string storeId);
        #endregion Asynchronous Operations
    }

    /// <summary>
    /// Represents a collection of functions to interact with the API endpoints
    /// </summary>
    public class VirtoCommerceGoogleEcommerceAnalyticsApi : IVirtoCommerceGoogleEcommerceAnalyticsApi
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VirtoCommerceGoogleEcommerceAnalyticsApi"/> class
        /// using Configuration object
        /// </summary>
        /// <param name="apiClient">An instance of ApiClient.</param>
        /// <returns></returns>
        public VirtoCommerceGoogleEcommerceAnalyticsApi(ApiClient apiClient)
        {
            ApiClient = apiClient;
            Configuration = apiClient.Configuration;
        }

        /// <summary>
        /// Gets the base path of the API client.
        /// </summary>
        /// <value>The base path</value>
        public string GetBasePath()
        {
            return ApiClient.RestClient.BaseUrl.ToString();
        }

        /// <summary>
        /// Gets or sets the configuration object
        /// </summary>
        /// <value>An instance of the Configuration</value>
        public Configuration Configuration { get; set; }

        /// <summary>
        /// Gets or sets the API client object
        /// </summary>
        /// <value>An instance of the ApiClient</value>
        public ApiClient ApiClient { get; set; }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>StoreSettings</returns>
        public StoreSettings StoreSettingsGetSettings(string storeId)
        {
             ApiResponse<StoreSettings> localVarResponse = StoreSettingsGetSettingsWithHttpInfo(storeId);
             return localVarResponse.Data;
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>ApiResponse of StoreSettings</returns>
        public ApiResponse<StoreSettings> StoreSettingsGetSettingsWithHttpInfo(string storeId)
        {
            // verify the required parameter 'storeId' is set
            if (storeId == null)
                throw new ApiException(400, "Missing required parameter 'storeId' when calling VirtoCommerceGoogleEcommerceAnalyticsApi->StoreSettingsGetSettings");

            var localVarPath = "/api/ga/{storeId}/settings";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new Dictionary<string, string>();
            var localVarHeaderParams = new Dictionary<string, string>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            object localVarPostBody = null;

            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json", 
                "text/json", 
                "application/xml", 
                "text/xml"
            };
            string localVarHttpHeaderAccept = ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (storeId != null) localVarPathParams.Add("storeId", ApiClient.ParameterToString(storeId)); // path parameter


            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse)ApiClient.CallApi(localVarPath,
                Method.GET, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int)localVarResponse.StatusCode;

            if (localVarStatusCode >= 400 && (localVarStatusCode != 404 || Configuration.ThrowExceptionWhenStatusCodeIs404))
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsGetSettings: " + localVarResponse.Content, localVarResponse.Content);
            else if (localVarStatusCode == 0)
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsGetSettings: " + localVarResponse.ErrorMessage, localVarResponse.ErrorMessage);

            return new ApiResponse<StoreSettings>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (StoreSettings)ApiClient.Deserialize(localVarResponse, typeof(StoreSettings)));
            
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>Task of StoreSettings</returns>
        public async System.Threading.Tasks.Task<StoreSettings> StoreSettingsGetSettingsAsync(string storeId)
        {
             ApiResponse<StoreSettings> localVarResponse = await StoreSettingsGetSettingsAsyncWithHttpInfo(storeId);
             return localVarResponse.Data;

        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="storeId"></param>
        /// <returns>Task of ApiResponse (StoreSettings)</returns>
        public async System.Threading.Tasks.Task<ApiResponse<StoreSettings>> StoreSettingsGetSettingsAsyncWithHttpInfo(string storeId)
        {
            // verify the required parameter 'storeId' is set
            if (storeId == null)
                throw new ApiException(400, "Missing required parameter 'storeId' when calling VirtoCommerceGoogleEcommerceAnalyticsApi->StoreSettingsGetSettings");

            var localVarPath = "/api/ga/{storeId}/settings";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new Dictionary<string, string>();
            var localVarHeaderParams = new Dictionary<string, string>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            object localVarPostBody = null;

            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json", 
                "text/json", 
                "application/xml", 
                "text/xml"
            };
            string localVarHttpHeaderAccept = ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (storeId != null) localVarPathParams.Add("storeId", ApiClient.ParameterToString(storeId)); // path parameter


            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse)await ApiClient.CallApiAsync(localVarPath,
                Method.GET, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int)localVarResponse.StatusCode;

            if (localVarStatusCode >= 400 && (localVarStatusCode != 404 || Configuration.ThrowExceptionWhenStatusCodeIs404))
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsGetSettings: " + localVarResponse.Content, localVarResponse.Content);
            else if (localVarStatusCode == 0)
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsGetSettings: " + localVarResponse.ErrorMessage, localVarResponse.ErrorMessage);

            return new ApiResponse<StoreSettings>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                (StoreSettings)ApiClient.Deserialize(localVarResponse, typeof(StoreSettings)));
            
        }
        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns></returns>
        public void StoreSettingsUpdate(StoreSettings settings, string storeId)
        {
             StoreSettingsUpdateWithHttpInfo(settings, storeId);
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns>ApiResponse of Object(void)</returns>
        public ApiResponse<object> StoreSettingsUpdateWithHttpInfo(StoreSettings settings, string storeId)
        {
            // verify the required parameter 'settings' is set
            if (settings == null)
                throw new ApiException(400, "Missing required parameter 'settings' when calling VirtoCommerceGoogleEcommerceAnalyticsApi->StoreSettingsUpdate");
            // verify the required parameter 'storeId' is set
            if (storeId == null)
                throw new ApiException(400, "Missing required parameter 'storeId' when calling VirtoCommerceGoogleEcommerceAnalyticsApi->StoreSettingsUpdate");

            var localVarPath = "/api/ga/{storeId}/settings";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new Dictionary<string, string>();
            var localVarHeaderParams = new Dictionary<string, string>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            object localVarPostBody = null;

            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
                "application/json", 
                "text/json", 
                "application/xml", 
                "text/xml", 
                "application/x-www-form-urlencoded"
            };
            string localVarHttpContentType = ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
            };
            string localVarHttpHeaderAccept = ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (storeId != null) localVarPathParams.Add("storeId", ApiClient.ParameterToString(storeId)); // path parameter
            if (settings.GetType() != typeof(byte[]))
            {
                localVarPostBody = ApiClient.Serialize(settings); // http body (model) parameter
            }
            else
            {
                localVarPostBody = settings; // byte array
            }


            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse)ApiClient.CallApi(localVarPath,
                Method.POST, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int)localVarResponse.StatusCode;

            if (localVarStatusCode >= 400 && (localVarStatusCode != 404 || Configuration.ThrowExceptionWhenStatusCodeIs404))
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsUpdate: " + localVarResponse.Content, localVarResponse.Content);
            else if (localVarStatusCode == 0)
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsUpdate: " + localVarResponse.ErrorMessage, localVarResponse.ErrorMessage);

            
            return new ApiResponse<object>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                null);
        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns>Task of void</returns>
        public async System.Threading.Tasks.Task StoreSettingsUpdateAsync(StoreSettings settings, string storeId)
        {
             await StoreSettingsUpdateAsyncWithHttpInfo(settings, storeId);

        }

        /// <summary>
        ///  
        /// </summary>
        /// <exception cref="VirtoCommerce.GoogleEcommerceAnalyticsModule.Client.Client.ApiException">Thrown when fails to make API call</exception>
        /// <param name="settings"></param>
        /// <param name="storeId"></param>
        /// <returns>Task of ApiResponse</returns>
        public async System.Threading.Tasks.Task<ApiResponse<object>> StoreSettingsUpdateAsyncWithHttpInfo(StoreSettings settings, string storeId)
        {
            // verify the required parameter 'settings' is set
            if (settings == null)
                throw new ApiException(400, "Missing required parameter 'settings' when calling VirtoCommerceGoogleEcommerceAnalyticsApi->StoreSettingsUpdate");
            // verify the required parameter 'storeId' is set
            if (storeId == null)
                throw new ApiException(400, "Missing required parameter 'storeId' when calling VirtoCommerceGoogleEcommerceAnalyticsApi->StoreSettingsUpdate");

            var localVarPath = "/api/ga/{storeId}/settings";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new Dictionary<string, string>();
            var localVarHeaderParams = new Dictionary<string, string>(Configuration.DefaultHeader);
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            object localVarPostBody = null;

            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
                "application/json", 
                "text/json", 
                "application/xml", 
                "text/xml", 
                "application/x-www-form-urlencoded"
            };
            string localVarHttpContentType = ApiClient.SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
            };
            string localVarHttpHeaderAccept = ApiClient.SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            // set "format" to json by default
            // e.g. /pet/{petId}.{format} becomes /pet/{petId}.json
            localVarPathParams.Add("format", "json");
            if (storeId != null) localVarPathParams.Add("storeId", ApiClient.ParameterToString(storeId)); // path parameter
            if (settings.GetType() != typeof(byte[]))
            {
                localVarPostBody = ApiClient.Serialize(settings); // http body (model) parameter
            }
            else
            {
                localVarPostBody = settings; // byte array
            }


            // make the HTTP request
            IRestResponse localVarResponse = (IRestResponse)await ApiClient.CallApiAsync(localVarPath,
                Method.POST, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            int localVarStatusCode = (int)localVarResponse.StatusCode;

            if (localVarStatusCode >= 400 && (localVarStatusCode != 404 || Configuration.ThrowExceptionWhenStatusCodeIs404))
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsUpdate: " + localVarResponse.Content, localVarResponse.Content);
            else if (localVarStatusCode == 0)
                throw new ApiException(localVarStatusCode, "Error calling StoreSettingsUpdate: " + localVarResponse.ErrorMessage, localVarResponse.ErrorMessage);

            
            return new ApiResponse<object>(localVarStatusCode,
                localVarResponse.Headers.ToDictionary(x => x.Name, x => x.Value.ToString()),
                null);
        }
    }
}
