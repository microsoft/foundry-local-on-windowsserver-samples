namespace PatientSummaryTool.Models.Objects
{
    public class Patient
    {
        private int id;
        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        private string firstName;
        public string FirstName
        {
            get { return firstName; }
            set { firstName = value; }
        }

        private string lastName;
        public string LastName
        {
            get { return lastName; }
            set { lastName = value; }
        }

        private bool isTranslationCompleted;
        public bool IsTranslationCompleted
        {
            get { return isTranslationCompleted; }
            set { isTranslationCompleted = value; }
        }

        private string sourceLanguage;
        public string SourceLanguage
        {
            get { return sourceLanguage; }
            set { sourceLanguage = value; }
        }

        public override string ToString()
        {
            return FirstName + " " + LastName;
        }
    }
}
