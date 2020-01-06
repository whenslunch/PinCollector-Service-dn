using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

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
            outputs.Add(await context.CallActivityAsync<string>("CreateItemDf_ResizeUploadThumbnail", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
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
                PartitionKey = "tzelin-hrcpin",
                RowKey = pitem.id,
                City = pitem.city,
                Country = pitem.country
            };

            TableOperation tableOp = TableOperation.Insert(collectionTableItem);
            collectionsTable.ExecuteAsync(tableOp);
            
            return $"Hello new pin!";
        }

        [FunctionName("CreateItemDf_UploadFullSizeImage")]
        public static async Task<string> UploadFullSizeImage([ActivityTrigger] PinItem pitem, ILogger log)
        {
            log.LogInformation($"Uploading full size image of {pitem.id}.");

            
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("collectionsConnectionString"));
            CloudBlobClient client = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("pinimages");

            if (await container.ExistsAsync())
            { 
                log.LogInformation("Found Blob Container.");

                string fileExt = pitem.imgtype.Split('/')[1];
                string fileName = pitem.id + "." + fileExt;
                CloudBlockBlob blob = container.GetBlockBlobReference(fileName);
                blob.Properties.ContentType = pitem.imgtype;
               
                await blob.UploadFromByteArrayAsync(pitem.image, 0, pitem.image.Length);    
            }



            return $"Hello {pitem.id} new image!";
        }

        [FunctionName("CreateItemDf_ResizeUploadThumbnail")]
        public static string ResizeUploadThumbnail([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        public class CollectionsTableEntity : TableEntity
    {
        // this is inherited from TableEntity, so don't need to define it
        // public string RowKey { get; set; }  
        public string Country { get; set; }
        public string City { get; set; }
    }

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

            Guid id = Guid.NewGuid();
            pinitem.id = id.ToString();
            pinitem.country = await memstream.Contents[0].ReadAsStringAsync();
            pinitem.city = await memstream.Contents[1].ReadAsStringAsync();
            pinitem.image = await memstream.Contents[2].ReadAsByteArrayAsync();

            pinitem.imgtype = memstream.Contents[2].Headers.ContentType.ToString();

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("CreateItemDf_Orch", pinitem);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);

            
        }
    }
}