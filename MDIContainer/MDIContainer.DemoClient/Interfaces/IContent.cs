namespace MDIContainer.DemoClient.Interfaces
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;

   public interface IContent
   {
      string Title { get; }
      bool CanClose { get; }
   }
}
