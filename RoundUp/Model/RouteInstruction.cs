using System.ComponentModel;
using System.Runtime.CompilerServices;
using RArcher.Phone.Toolkit.Common;
using RoundUp.Annotations;

namespace RoundUp.Model
{
    /// <summary>Defines an instruction for getting to the roundup point</summary>
    public class RouteInstruction : IAutoSaveRestore, INotifyPropertyChanged
    {
        private string _instruction;

        public string Instruction
        {
            get { return _instruction; }
            set { _instruction = value; OnPropertyChanged(); }
        }

        /// <summary>Flattens an instance of the object to a string that can be saved to app state or isolated storage</summary>
        /// <returns>Returns a flattened instance of the object that can be saved to app state or isolated storage</returns>
        public string ToStringRepresentation()
        {
            return Instruction;
        }

        /// <summary>Repopulates the object from a flattened string representation of its properties</summary>
        /// <param name="sObject">A flat string representation of the object's properties</param>
        /// <returns>Returns true if the object's properties were successfully rehydrated from a flattened string representation</returns>
        public object FromStringRepresentation(string sObject)
        {
            if(string.IsNullOrEmpty(sObject)) return null;  // Signal we don't want this item added to the collection

            Instruction = sObject;
            return this;
        }

        /// <summary>Returns a string that represents the current object</summary>
        /// <returns>A string that represents the current object</returns>
        public override string ToString()
        {
            return Instruction ?? string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if(handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}