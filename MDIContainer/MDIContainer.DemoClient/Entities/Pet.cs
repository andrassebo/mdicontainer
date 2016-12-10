namespace MDIContainer.DemoClient.Entities
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;

   using MDIContainer.DemoClient.Bases;

   public class Pet : ViewModelBase
   {
      public Pet(string name, string owner)
      {
         this.Name = name;
         this.Owner = owner;
      }

      public string Name { get; set; }
      public string Owner { get; set; }
   }
}
