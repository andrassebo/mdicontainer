namespace MDIContainer.DemoClient.Entities
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;

   using MDIContainer.DemoClient.Bases;

   public class Person : ViewModelBase
   {
      public event EventHandler? Changed;

      public Person(string name, DateTime birthDate, string address)
      {
         this.Name = name;
         this.BirthDate = birthDate;
         this.Address = address;
      }

      private string _name = string.Empty;
      public string Name
      {
         get { return _name; }
         set
         {
            if (SetProperty(ref _name, value))
               this.Changed?.Invoke(this, EventArgs.Empty);
         }
      }

      private DateTime _birthDate;
      public DateTime BirthDate
      {
         get { return _birthDate; }
         set
         {
            if (SetProperty(ref _birthDate, value))
               this.Changed?.Invoke(this, EventArgs.Empty);
         }
      }

      private string _address = string.Empty;
      public string Address
      {
         get { return _address; }
         set
         {
            if (SetProperty(ref _address, value))
               this.Changed?.Invoke(this, EventArgs.Empty);
         }
      }
   }
}
