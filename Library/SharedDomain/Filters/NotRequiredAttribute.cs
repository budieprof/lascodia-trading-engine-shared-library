using System.ComponentModel.DataAnnotations;

namespace Lascodia.Trading.Engine.SharedDomain.Filters;

    public class NotRequiredAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            return true;
        }

        protected override ValidationResult IsValid(object? value, ValidationContext context)
        {
            return ValidationResult.Success;
        }
    }
