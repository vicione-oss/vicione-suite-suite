using Core.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Blazor.Server.Backend.Extensions;

public static class UserMessageExtensions
{
    extension(ITempDataDictionaryFactory tempDataFactory)
    {
        private void AddUserMessage(HttpContext context,
            string key,
            object message)
        {
            var tempData = tempDataFactory.GetTempData(context);
            tempData[key] = message;
            tempData.Save();
        }

        /// <summary>
        /// Adds an external login error to TempData for display after redirect.
        /// </summary>
        /// <remarks>
        /// Used to pass error state from minimal API endpoints to the login page,
        /// since Blazor components don't support <c>[TempData]</c> attribute binding.
        /// </remarks>
        public void AddExternalError(HttpContext context,
            object message)
        {
            tempDataFactory.AddUserMessage(context, 
                ExternalLoginErrorConstants.ExternalLoginErrorKey, 
                message);
        }
    }
}
