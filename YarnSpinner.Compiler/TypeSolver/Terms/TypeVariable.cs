namespace TypeChecker
{

    public class TypeVariable : TypeTerm
    {
        public string Name { get; set; }

        public TypeVariable(string name)
        {
            Name = name;
        }

        public override string ToString() => Name;

        public override TypeTerm Substitute(Substitution s)
        {
            if (s.ContainsKey(this))
            {
                return s[this].Substitute(s);
            }
            else
            {
                return this;
            }
        }

        public override bool Equals(TypeTerm other)
        {
            return other is TypeVariable otherVariable && otherVariable.Name == Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static implicit operator TypeVariable(string input)
        {
            return new TypeVariable(input);
        }
    }

}
