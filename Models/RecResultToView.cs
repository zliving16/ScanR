using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
// using ReceiptScanner.Models;

namespace ReceiptScanner.Models
{
    public class RecResultToView
    {
        public List<ItemToView> ItemsList { get; set; }
        public string StoreName { get; set; }
        public DateTime Date { get; set; }
    }
}