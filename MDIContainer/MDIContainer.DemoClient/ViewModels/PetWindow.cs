namespace MDIContainer.DemoClient.ViewModels
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;

   using MDIContainer.DemoClient.Bases;
   using MDIContainer.DemoClient.Entities;
   using MDIContainer.DemoClient.Interfaces;

   public class PetWindow : ViewModelBase, IContent
   {
      public string Title
      {
         get { return string.Format("{0} - {1}", Pet.Name, Pet.Owner); }
      }

      public PetWindow(Pet pet)
      {
         this.Pet = pet;
      }

      public Pet Pet { get; private set; }

      public bool CanClose
      {
         get { return true; }
      }
   }
}
