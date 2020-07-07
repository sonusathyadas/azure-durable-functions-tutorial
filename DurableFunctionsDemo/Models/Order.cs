using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DurableFunctionsDemo.Models
{
    [Table("Orders")]
    public class Order
    {
        [JsonProperty("id")]        
        public int Id { get; set; }

        [JsonProperty("customerName")]        
        public string CustomerName { get; set; }

        [JsonProperty("amount")]        
        public double Amount { get; set; }

        [JsonProperty("orderDate")]        
        public DateTime OrderDate { get; set; }

        [JsonProperty("deliveryDate")]        
        public DateTime DeliveryDate { get; set; }

        [JsonProperty("email")]      
        public string Email { get; set; }
    }
}
