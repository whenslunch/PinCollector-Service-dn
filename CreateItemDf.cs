using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

namespace PinCollector.CreateItem
{
    public static class CreateItemDf
    {

        // Orchestrator

        [FunctionName("CreateItemDf_Orch")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            PinItem pinitem = context.GetInput<PinItem>();

            outputs.Add(await context.CallActivityAsync<string>("CreateItemDf_CreateNewTableEntry", pinitem));
            outputs.Add(await context.CallActivityAsync<string>("CreateItemDf_UploadFullSizeImage", pinitem));
            outputs.Add(await context.CallActivityAsync<string>("CreateItemDf_ResizeUploadThumbnail", pinitem));

            return outputs;
        }

        // Worker activity

        [FunctionName("CreateItemDf_CreateNewTableEntry")]
        public static string CreateNewTableEntry(
            [ActivityTrigger] PinItem pitem, 
            [Table("Collections")] CloudTable collectionsTable, ILogger log
        )
        {
            log.LogInformation($"Going to create a new entry of {pitem.id}, {pitem.city}, {pitem.country}.");

            CollectionsTableEntity collectionTableItem = new CollectionsTableEntity(){
                PartitionKey = "tzelin-hrcpin",   // TODO: get into an env variable 
                RowKey = pitem.id,
                City = pitem.city,
                Country = pitem.country
            };

            TableOperation tableOp = TableOperation.Insert(collectionTableItem);
            collectionsTable.ExecuteAsync(tableOp);
            
            return "Hello new pin!";
        }

        [FunctionName("CreateItemDf_UploadFullSizeImage")]
        public static async Task<string> UploadFullSizeImage([ActivityTrigger] PinItem pitem, ILogger log)
        {
            log.LogInformation($"Uploading full size image of {pitem.id}.");

            // Create objects for Cloud Storage Account, Blob Client, Blob Container
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("collectionsConnectionString"));
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("pinimages");

            if (await container.ExistsAsync())
            { 
                log.LogInformation("Found Blob Container.");

                // Extract the file extension
                string fileExt = pitem.imgtype.Split('/')[1];
                string fileName = pitem.id + "." + fileExt;

                // Set up the upload operation
                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
                blob.Properties.ContentType = pitem.imgtype;
               
               // Upload the file
                await blob.UploadFromByteArrayAsync(pitem.image, 0, pitem.image.Length);    
            } 

            return $"Hello {pitem.id} new image!";
        }


        private static IImageEncoder GetEncoder(string extension)
        {
            IImageEncoder encoder = null;

            //somehow IgnoreCase below didn't catch JPG so adding ToLower() as insurance
            //extension = extension.Replace(".", "").ToLower();  

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


        [FunctionName("CreateItemDf_ResizeUploadThumbnail")]
        public static async Task<string> ResizeUploadThumbnail([ActivityTrigger] PinItem pitem, ILogger log)
        {
            log.LogInformation($"Resize and Upload Thumbnail.");

            var imageextension = pitem.imgtype.Split('/')[1];
            var encoder = GetEncoder(imageextension);

            if (encoder != null)
            {

               var thumbnailWidth = Convert.ToInt32(Environment.GetEnvironmentVariable("THUMBNAIL_WIDTH"));
                         
                using (var output = new MemoryStream())
                using (Image<Rgba32> img = (Image<Rgba32>)Image.Load(pitem.image))
                {

                    // do the image resizing and Image<Rgba32> / MemoryStream / byte array manipulations
                    var divisor = img.Width / thumbnailWidth;
                    var height = Convert.ToInt32(Math.Round((decimal)(img.Height/divisor)));
                    img.Mutate( x => x.Resize(thumbnailWidth, height));
                    img.Save(output, encoder);
                    output.Position = 0;    
                    byte[] imageba = output.ToArray();

                    // Create objects for Cloud Storage Account, Blob Client, Blob Container
                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("collectionsConnectionString"));
                    CloudBlobClient client = storageAccount.CreateCloudBlobClient();
                    CloudBlobContainer container = client.GetContainerReference("pinthumbs");

                    if (await container.ExistsAsync())
                    { 
                        // Get the file extension.
                        string fileName = pitem.id + ".thumb." + imageextension;

                        // Set up the upload operation
                        CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
                        blob.Properties.ContentType = pitem.imgtype;
                    
                        // Upload the image
                        await blob.UploadFromByteArrayAsync(imageba, 0, imageba.Length);   
                        
                    }
                }
            }
            else
            {
                log.LogInformation("Image Type not supported.");
            }        

            return $"Hello {pitem.id} new thumbnail!";
        }


        // This class is the  Table Entity used for the Azure Storage Table insert.
        public class CollectionsTableEntity : TableEntity
        {
            // this is inherited from TableEntity, so don't need to define it
            // public string RowKey { get; set; }  
            public string Country { get; set; }
            public string City { get; set; }
        }

        // Class of objects used to pass data about the new entry through the whole Durable Function.
        public class PinItem
        {
            public string id { get; set; }
            public string city { get; set; }
            public string country { get; set; }
            public byte[] image { get; set; }
            public string imgtype { get; set; }
        }

        // HTTP trigger - Starter

        [FunctionName("CreateItemDf")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            MultipartMemoryStreamProvider memstream =  await req.Content.ReadAsMultipartAsync();  
            PinItem pinitem = new PinItem();

            // Fill in all the fields for the internal object which will be passed to the Orchestrator
            //   and then downstream to all Activities.

            Guid id = Guid.NewGuid();
            pinitem.id = id.ToString();
            pinitem.country = await memstream.Contents[0].ReadAsStringAsync();
            pinitem.city = await memstream.Contents[1].ReadAsStringAsync();
            pinitem.image = await memstream.Contents[2].ReadAsByteArrayAsync();
            pinitem.imgtype = memstream.Contents[2].Headers.ContentType.ToString();

            // Pass the PinItem object as input downstream as the Orchestrator is kicked off.

            string instanceId = await starter.StartNewAsync("CreateItemDf_Orch", pinitem);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);

            
        }
    }
}