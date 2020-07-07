using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DurableFunctionsDemo.Models
{
    [Table("Payments")]
    public class Payment
    {        
        public int Id { get; set; }
      
        public int OrderId { get; set; }
        
        public string PaymentMode { get; set; }
        
        public string PaymentStatus { get; set; }

        public double Amount { get; set; }
    }
}
