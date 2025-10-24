using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PatientSummaryTool.Utils
{
    public class ValidatableBindableBase : BindableBase, INotifyDataErrorInfo
    {
        Dictionary<string, List<string>> errors = new Dictionary<string, List<string>>();

        public virtual bool HasErrors
        {
            get { return errors.Count > 0; }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged = delegate { };

        public virtual IEnumerable GetErrors(string propertyName)
        {
            if (errors.TryGetValue(propertyName, out List<string> value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }

        protected override void SetProperty<T>(ref T member, T val, [CallerMemberName] string propertyName = null)
        {
            base.SetProperty<T>(ref member, val, propertyName);
            ValidateProperty(propertyName, val);
        }

        private void ValidateProperty<T>(string propertyName, T val)
        {
            var results = new List<ValidationResult>();
            ValidationContext context = new ValidationContext(this);
            context.MemberName = propertyName;
            Validator.TryValidateProperty(val, context, results);

            if (results.Any())
            {
                errors[propertyName] = results.Select(x => x.ErrorMessage).ToList();
            }
            else
            {
                errors.Remove(propertyName);
            }

            ErrorsChanged(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }
}
