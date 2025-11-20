using System;
using System.Collections.Generic;


namespace Taxxor.Project
{


    /// <summary>
    /// Input validation helpers
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Validates a set
        /// </summary>
        public class InputValidationCollection
        {
            public List<InputValidationSet> Sets = new List<InputValidationSet>();

            public InputValidationCollection()
            {

            }

            public void Add(InputValidationSet inputValidationSet)
            {
                this.Sets.Add(inputValidationSet);
            }

            public void Add(KeyValuePair<string, string> keyValue, RegexEnum regexEnum, bool generateErrorOnEmpty)
            {
                this.Sets.Add(new InputValidationSet(keyValue, regexEnum, generateErrorOnEmpty));
            }

            public void Add(KeyValuePair<string, string> keyValue, string regexString, bool generateErrorOnEmpty)
            {
                this.Sets.Add(new InputValidationSet(keyValue, regexString, generateErrorOnEmpty));
            }

            public void Add(KeyValuePair<string, string> keyValue, RegexEnum regexEnum, string defaultValue)
            {
                this.Sets.Add(new InputValidationSet(keyValue, regexEnum, defaultValue));
            }

            public void Add(KeyValuePair<string, string> keyValue, string regexString, string defaultValue)
            {
                this.Sets.Add(new InputValidationSet(keyValue, regexString, defaultValue));
            }


            public void Add(string key, string value, RegexEnum regexEnum, bool generateErrorOnEmpty)
            {
                this.Sets.Add(new InputValidationSet(key, value, regexEnum, generateErrorOnEmpty));
            }

            public void Add(string key, string value, string regexString, bool generateErrorOnEmpty)
            {
                this.Sets.Add(new InputValidationSet(key, value, regexString, generateErrorOnEmpty));
            }

            public void Add(ref KeyValuePair<string, string> keyValue, RegexEnum regexEnum, string defaultValue)
            {
                this.Sets.Add(new InputValidationSet(ref keyValue, regexEnum, defaultValue));
            }

            public void Add(ref KeyValuePair<string, string> keyValue, string regexString, string defaultValue)
            {
                this.Sets.Add(new InputValidationSet(ref keyValue, regexString, defaultValue));
            }


            public void Add(ref string key, ref string value, RegexEnum regexEnum, bool generateErrorOnEmpty)
            {
                this.Sets.Add(new InputValidationSet(ref key, ref value, regexEnum, generateErrorOnEmpty));
            }

            public void Add(ref string key, ref string value, string regexString, bool generateErrorOnEmpty)
            {
                this.Sets.Add(new InputValidationSet(ref key, ref value, regexString, generateErrorOnEmpty));
            }

            public void Add(ref string key, ref string value, RegexEnum regexEnum, string defaultValue)
            {
                this.Sets.Add(new InputValidationSet(ref key, ref value, regexEnum, defaultValue));
            }

            public void Add(ref string key, ref string value, string regexString, string defaultValue)
            {
                this.Sets.Add(new InputValidationSet(ref key, ref value, regexString, defaultValue));
            }




            public TaxxorReturnMessage Validate()
            {
                var successfullyValidated = true;
                TaxxorReturnMessage? erroredValidation = null;
                foreach (InputValidationSet validationSet in this.Sets)
                {
                    try
                    {
                        var validationResult = validationSet.Validate();
                        if (!validationResult.Success)
                        {
                            successfullyValidated = false;
                            erroredValidation = new TaxxorReturnMessage(false, validationResult.Message, validationResult.DebugInfo);
                        }
                        else
                        {
                            if (validationResult.Message == "Using default value")
                            {
                                //validationSet.Value = validationResult.Payload;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        successfullyValidated = false;
                        erroredValidation = new TaxxorReturnMessage(false, $"There was a problem validating set. Key: {validationSet.Key}, Value: {validationSet.Value}", ex.ToString());
                    }
                }

                if (successfullyValidated)
                {
                    return new TaxxorReturnMessage(true, $"Successfully validated", $"Sets.Count: {this.Sets.Count.ToString()}");
                }
                else
                {
                    return erroredValidation;
                }
            }
        }


        /// <summary>
        /// Defines a single key-value combination to be checked against a regular expression
        /// </summary>
        public class InputValidationSet
        {
            public string Key { get; set; } = null;
            public string Value { get; set; } = null;
            private string _dafaultValue { get; set; } = null;
            public RegexEnum RegexEnum { get; set; } = null;
            public string RegexString { get; set; } = null;
            public bool GenerateErrorOnEmpty { get; set; } = false;


            public InputValidationSet(KeyValuePair<string, string> keyValue, RegexEnum regexEnum, bool generateErrorOnEmpty)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexEnum = regexEnum;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }

            public InputValidationSet(KeyValuePair<string, string> keyValue, string regexString, bool generateErrorOnEmpty)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexString = regexString;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }

            public InputValidationSet(KeyValuePair<string, string> keyValue, RegexEnum regexEnum, string defaultValue)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexEnum = regexEnum;
                this._dafaultValue = defaultValue;
            }

            public InputValidationSet(KeyValuePair<string, string> keyValue, string regexString, string defaultValue)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexString = regexString;
                this._dafaultValue = defaultValue;
            }


            public InputValidationSet(ref KeyValuePair<string, string> keyValue, RegexEnum regexEnum, bool generateErrorOnEmpty)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexEnum = regexEnum;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }

            public InputValidationSet(ref KeyValuePair<string, string> keyValue, string regexString, bool generateErrorOnEmpty)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexString = regexString;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }


            public InputValidationSet(ref KeyValuePair<string, string> keyValue, RegexEnum regexEnum, string defaultValue)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexEnum = regexEnum;
                this._dafaultValue = defaultValue;
            }

            public InputValidationSet(ref KeyValuePair<string, string> keyValue, string regexString, string defaultValue)
            {
                this.Key = keyValue.Key;
                this.Value = keyValue.Value;
                this.RegexString = regexString;
                this._dafaultValue = defaultValue;
            }

            public InputValidationSet(string key, string value, RegexEnum regexEnum, bool generateErrorOnEmpty)
            {
                this.Key = key;
                this.Value = value;
                this.RegexEnum = regexEnum;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }

            public InputValidationSet(string key, string value, string regexString, bool generateErrorOnEmpty)
            {
                this.Key = key;
                this.Value = value;
                this.RegexString = regexString;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }

            public InputValidationSet(string key, ref string value, RegexEnum regexEnum, string defaultValue)
            {
                this.Key = key;
                this.Value = value;
                this.RegexEnum = regexEnum;
                this._dafaultValue = defaultValue;
            }

            public InputValidationSet(string key, ref string value, string regexString, string defaultValue)
            {
                this.Key = key;
                this.Value = value;
                this.RegexString = regexString;
                this._dafaultValue = defaultValue;
            }


            public InputValidationSet(ref string key, ref string value, RegexEnum regexEnum, bool generateErrorOnEmpty)
            {
                this.Key = key;
                this.Value = value;
                this.RegexEnum = regexEnum;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }

            public InputValidationSet(ref string key, ref string value, string regexString, bool generateErrorOnEmpty)
            {
                this.Key = key;
                this.Value = value;
                this.RegexString = regexString;
                this.GenerateErrorOnEmpty = generateErrorOnEmpty;
            }

            public InputValidationSet(ref string key, ref string value, RegexEnum regexEnum, string defaultValue)
            {
                this.Key = key;
                this.Value = value;
                this.RegexEnum = regexEnum;
                this._dafaultValue = defaultValue;
            }

            public InputValidationSet(ref string key, ref string value, string regexString, string defaultValue)
            {
                this.Key = key;
                this.Value = value;
                this.RegexString = regexString;
                this._dafaultValue = defaultValue;
            }

            public TaxxorReturnMessage Validate()
            {
                if (this.Key == null) return new TaxxorReturnMessage(false, "Error - Key was null");
                if (this.Key == "") return new TaxxorReturnMessage(false, "Error - Key was empty string");
                if (this.GenerateErrorOnEmpty)
                {
                    if (this.Value == null) return new TaxxorReturnMessage(false, "Error - value was null", $"Key: '{this.Key}'");
                    if (this.Value == "") return new TaxxorReturnMessage(false, "Error - value was empty string", $"Key: '{this.Key}'");
                }
                else
                {

                    if (this.Value == null || this.Value == "")
                    {
                        if (this._dafaultValue != null)
                        {
                            this.Value = this._dafaultValue;
                            return new TaxxorReturnMessage(true, "Using default value", this._dafaultValue, $"Key: '{this.Key}' with Value '{this.Value}'");
                        }
                        else
                        {
                            return new TaxxorReturnMessage(true, "Empty value is allowed", $"Key: '{this.Key}' with Value {((this.Value==null)?"null":"''")}");
                        }
                    }
                }

                if (this.RegexEnum == null && string.IsNullOrEmpty(RegexString))
                {
                    return new TaxxorReturnMessage(false, "Error - no validation pattern was supplied", $"Key: '{this.Key}' with Value '{this.Value}'");
                }

                // Retrieve the regular expression as a string
                var validationPattern = (string.IsNullOrEmpty(RegexString)) ? RegexEnum.Value : RegexString;
                try
                {
                    if (Framework.RegExpTest(validationPattern, this.Value, true))
                    {
                        return new TaxxorReturnMessage(true, $"Successfully validated", $"Key: '{this.Key}' with Value '{this.Value}'");
                    }
                    else
                    {
                        return new TaxxorReturnMessage(false, $"Error - Key: '{this.Key}' with Value '{this.Value}' did not pass validation", $"validationPattern: '{validationPattern}'");
                    }
                }
                catch (Exception ex)
                {

                    return new TaxxorReturnMessage(false, $"There was a problem validating set. Key: {this.Key}, Value: {this.Value}", ex.ToString());
                }

            }

        }


    }
}