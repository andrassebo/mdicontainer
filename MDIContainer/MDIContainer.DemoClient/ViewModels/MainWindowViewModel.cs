namespace MDIContainer.DemoClient.ViewModels
{
   using System.Collections.Generic;
   using System.Collections.ObjectModel;
   using System.ComponentModel;

   using MDIContainer.DemoClient.Bases;
   using MDIContainer.DemoClient.Commands;
   using MDIContainer.DemoClient.Entities;
   using MDIContainer.DemoClient.Interfaces;

   public class MainWindowViewModel : ViewModelBase
   {
      public ObservableCollection<IContent> Items { get; private set; }

      public ObservableCollection<Person> People { get; private set; }

      public ObservableCollection<Pet> Pets { get; private set; }

      public RelayCommand ShowCommand { get; private set; }
      public RelayCommand ShowPetCommand { get; private set; }

      private IContent _selectedWindow = null;      
      public IContent SelectedWindow
      {
         get
         {
            return this._selectedWindow;
         }
         set
         {            
            this._selectedWindow = value;
            this.RaisePropertyChanged("SelectedWindow");
         }
      }

      public MainWindowViewModel()
      {
         this.Items = new ObservableCollection<IContent>();
         this.People = new ObservableCollection<Person>();
         this.Pets = new ObservableCollection<Pet>();

         this.ShowCommand = new RelayCommand(ShowPerson, p => p != null);
         this.ShowPetCommand = new RelayCommand(ShowPet, p => p != null);

         this.People.Add(new Person("John Texas", new System.DateTime(1978, 12, 3), "NYC"));
         this.People.Add(new Person("Margareth Smith", new System.DateTime(1996, 4, 2), "Dallas"));
         this.People.Add(new Person("Jenny Happyday", new System.DateTime(1991, 5, 5), "TX"));
         this.People.Add(new Person("William Box", new System.DateTime(1966, 7, 3), "CA"));

         this.Pets.Add(new Pet("Rex", "Aunt Mary"));
         this.Pets.Add(new Pet("Rusty", "Oncle Bill"));
      }

      private void ShowPerson(object p)
      {
         var person = p as Person;
         if (person != null)
         {
            var item = new PersonWindow(person);
            item.Closing += (s, e) => this.Items.Remove(item);
            this.Items.Add(item);
         }
      }

      private void ShowPet(object p)
      {
         var pet = p as Pet;
         if (pet != null)
         {
            var item = new PetWindow(pet);            
            this.Items.Add(item);
         }
      }
   }
}
