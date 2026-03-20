using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderApp.Models
{
    public class Vendor
    {
        public int VendorId { get; set; }
        [Required]
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        public ICollection<PurchaseOrder> PurchaseOrders { get; set; }
    }
}
