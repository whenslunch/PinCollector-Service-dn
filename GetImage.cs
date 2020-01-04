using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace PinCollector.GetImage
{
    public static class GetImage
    {
        [FunctionName("GetImage")]
        public static HttpResponseMessage Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "GetImage/{imageid}")] HttpRequest req,
            [Blob("pinimages/{imageid}.JPG", FileAccess.Read)] Byte[] imagedata, ILogger log )
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var result = new HttpResponseMessage(HttpStatusCode.OK);

            result.Content = new ByteArrayContent( imagedata );
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            
            return result;
            
        }
    }
}

// string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
//             dynamic data = JsonConvert.DeserializeObject(requestBody);
//             name = name ?? data?.name;

//             return name != null
//                 ? (ActionResult)new OkObjectResult($"Hello, {name}")
//                 : new BadRequestObjectResult("Please pass a name on the query string or in the request body");