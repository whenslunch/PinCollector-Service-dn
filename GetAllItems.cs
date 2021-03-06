using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using System.Linq;
using System.Collections.Generic;

namespace PinCollector.GetAllItems
{
    
    public static class GetAllItems
    {
        //attribute - set the function name as a property
        [FunctionName("GetAllItems")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [Table("Collections")] CloudTable collectionsTable, ILogger log )
        {
            log.LogInformation("C# HTTP trigger function processed a request.");


            TableQuery<CollectionsTableEntity> query = new TableQuery<CollectionsTableEntity>();
            TableQuerySegment<CollectionsTableEntity> segment = await collectionsTable.ExecuteQuerySegmentedAsync(query, null);

            List<CollectionsTableEntity> tablelist =  segment.ToList();
            log.LogInformation("Counted: " + tablelist.Count() );

            // transform the objects returned so they only have the relevant fields
            // TODO: error condition
            return (ActionResult)new OkObjectResult(segment.Select(Mappings.ToPinItem));
        }
    }
    
    public class PinItem
    {
        public string ItemNumber;  // this is the rowkey
        public string Country { get; set; }
        public string City { get; set; }

    }
    public class CollectionsTableEntity : TableEntity
    {
        // this is inherited from TableEntity, so don't need to define it
        // public string RowKey { get; set; }  
        public string Country { get; set; }
        public string City { get; set; }
    }

    public static class Mappings
    {
        public static CollectionsTableEntity ToTableEntity(this PinItem pinitem)
        {
            return new CollectionsTableEntity()
            {
                PartitionKey = "tzelin-hrcpin",
                Country = pinitem.Country,
                City = pinitem.City,
                
            };
        }

        public static PinItem ToPinItem( this CollectionsTableEntity itemtable)
        {
            return new PinItem()
            {
                Country = itemtable.Country,
                City = itemtable.City,
                ItemNumber = itemtable.RowKey,
            };
        }
    }

}


//Connection = "collectionsappsa_STORAGE"

        //     // attribute - properties set; this is an http trigger with anon access, get, post, route (none at this point)
        //     // here are the req and log bindings too
        //     // So unlike JS/TS, where all the params are stored in JSON, in C# it's all defined here within the function.
        //     //   won't it get messy...?  yeesh

        //     [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log,
        //     [Blob("pinimages/{filename}", FileAccess.Read)] Stream myBlob

        //     )

        // {
        //     log.LogInformation("C# HTTP trigger function processed a request.");

        //     // read the http query is there is one by the name of "name"
        //     // i.e. http://localhost:7071/api/GetAllItems?name=jim
        //     string name = req.Query["name"];

        //     // read the input stream that is the request body. 
        //     // make this an async call because why?  JS doesn't need to do this, assumed it's all ready when the function triggers.
        //     // maybe just to be safe?
        //     string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
        //     // deserialize the request body, which is all Json. convert it into a .NET type            
        //     dynamic data = JsonConvert.DeserializeObject(requestBody);

        //     // ?? null-coalescing operator
        //     // returns LHS if null, else RHS
        //     name = name ?? data?.name;  //data is now nullable '?'

        //     // depending on whether name is empty or not, return an error or a greeting
        //     return name != null
        //         ? (ActionResult)new OkObjectResult($"Hello, {name}")   // I suppose this generates a status 200
        //         : new BadRequestObjectResult("Please pass a name on the query string or in the request body");  // I suppose this generates a status 400
        // }