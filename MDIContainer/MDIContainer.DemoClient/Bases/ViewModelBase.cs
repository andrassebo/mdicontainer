namespace MDIContainer.DemoClient.Bases
{
   using System;
   using System.Collections.Generic;
   using System.ComponentModel;
   using System.Linq;
   using System.Text;
   using System.Runtime.CompilerServices;

   public abstract class ViewModelBase : INotifyPropertyChanged
   {
      public event PropertyChangedEventHandler? PropertyChanged;

      protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
      {
         var handler = this.PropertyChanged;
         if (handler != null)
         {
            handler(this, new PropertyChangedEventArgs(propertyName));
         }
      }

      protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
      {
         if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

         field = value;
         OnPropertyChanged(propertyName);
         return true;
      }
   }
}
