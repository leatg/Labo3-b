//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MDB.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Login
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public System.DateTime LoginDate { get; set; }
        public System.DateTime LogoutDate { get; set; }
        public string IpAddress { get; set; }
    
        public virtual User User { get; set; }
    }
}
