using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PinCollector.CreateItem
{
    public static class CreateItem
    {
        private static IImageEncoder GetEncoder(string extension)
        {
            IImageEncoder encoder = null;

            //somehow IgnoreCase below didn't catch JPG so adding ToLower() as insurance
            extension = extension.Replace(".", "").ToLower();  

            var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            if (isSupported)
            {
                switch (extension)
                {
                    case "png":
                        encoder = new PngEncoder();
                        break;
                    case "jpg":
                        encoder = new JpegEncoder();
                        break;
                    case "jpeg":
                        encoder = new JpegEncoder();
                        break;
                    case "gif":
                        encoder = new GifEncoder();
                        break;
                    default:
                        break;
                }
            }

            return encoder;
        }

        [FunctionName("CreateItem")]
        public static HttpResponseMessage Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("CreateItem function invoked");

            //
            // Declare variables
            // ------------------------------------
            //
                        
            FormCollection reqCollection = (FormCollection) req.Form; 
            Dictionary<string, string> dict = new Dictionary<string, string>();
            var response = new HttpResponseMessage();

            //
            // Check for request validity, if not exit immediately
            // --------------------------------------------------------
            //

            
            // if there's more than one image, then there's a problem
            if (reqCollection.Files.Count != 1 )
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.ReasonPhrase = "Bad Request: Request should contain one and only one image.";
            
                return response;
            } 

            // if that one file is not an image file, there's a problem
            var contenttype = reqCollection.Files[0].ContentType;
            if (!contenttype.StartsWith("image"))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.ReasonPhrase = "Bad Request: Attached file is not an image.";
            
                return response;
            }

            // if request doesn't contain the correct keys then there's a problem
            if (!reqCollection.ContainsKey("country") || !reqCollection.ContainsKey("city"))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.ReasonPhrase = "Bad Request: Request doesn't contain the correct keys.";
            
                return response;
            } 

            foreach(var key in reqCollection.Keys)
            {
                string value = reqCollection[key.ToString()];

                if (String.IsNullOrEmpty(value))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    response.ReasonPhrase = "Bad Request: Request contains null value(s).";

                    return response;
                }
                else
                {
                    dict.Add(key, value);
                }
            }

            // string city, country;
            // dict.TryGetValue("city", out city);
            // dict.TryGetValue("country", out country);
            // log.LogInformation($"[CreateItem] Request: {city}, {country}" );

            //
            // Do the image resize
            // -------------------
            //

            var filename = reqCollection.Files[0].FileName;
            var extension = Path.GetExtension(filename);
            var encoder = GetEncoder(extension);

            if (encoder != null)
            {
                var thumbnailWidth = Convert.ToInt32(Environment.GetEnvironmentVariable("THUMBNAIL_WIDTH"));
                Stream inputimage = reqCollection.Files[0].OpenReadStream();
            
                using (var output = new MemoryStream())
                using (Image<Rgba32> img = (Image<Rgba32>)Image.Load(inputimage))
                {
                    var divisor = img.Width / thumbnailWidth;
                    var height = Convert.ToInt32(Math.Round((decimal)(img.Height/divisor)));
                    img.Mutate( x => x.Resize(thumbnailWidth, height));
                    img.Save(output, encoder);
                    output.Position = 0;
                    
                    byte[] outputba = output.ToArray();
                    response.Content = new ByteArrayContent(outputba);
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    
                    return response;

                }
            }
            else
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.ReasonPhrase = "Bad Request: Image type not supported.";
            
                return response;
            }

            //
            // Create a new entry in Collections table
            // -----------------------------------------------
            // - Generate GUID for the image as rowKey


            // 
            // Save the full-size image to blob container pinimage
            // ----------------------------------------------
            // - Use the GUID as the filename



            // 
            // Save the thumbnail to blob container pinthumb
            // ----------------------------------------------
            // - Use the GUID+"-thumb" as the filename


            

            
        }
    }
}
