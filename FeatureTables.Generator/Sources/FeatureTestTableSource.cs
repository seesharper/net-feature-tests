﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AshMind.Extensions;
using DependencyInjection.FeatureTables.Generator.Data;
using DependencyInjection.FeatureTests;
using DependencyInjection.FeatureTests.Adapters;
using DependencyInjection.FeatureTests.Documentation;
using DependencyInjection.FeatureTests.XunitSupport;

namespace DependencyInjection.FeatureTables.Generator.Sources {
    public class FeatureTestTableSource : IFeatureTableSource {
        public IEnumerable<FeatureTable> GetTables() {
            // potentially I could have used Xunit runners, but they are a bit annoying to get through NuGet
            var testGroups = typeof(BasicTests).Assembly.GetTypes()
                                                        .SelectMany(t => t.GetMethods())
                                                        .Where(m => m.IsDefined<ForEachFrameworkAttribute>(false))
                                                        .GroupBy(m => m.DeclaringType)
                                                        .OrderBy(g => this.GetDisplayOrder(g.Key))
                                                        .ToArray();

            foreach (var group in testGroups) {
                var groupSpecialCases = this.GetSpecialCases(group.Key);
                var features = group.ToDictionary(m => m, this.ConvertToFeature);
                var table = new FeatureTable(this.GetDisplayName(group.Key), Frameworks.List(), features.Values) {
                    Description = this.GetDescription(@group.Key)
                };

                foreach (var test in group.OrderBy(this.GetDisplayOrder)) {
                    var specialCases = this.GetSpecialCases(test);
                    specialCases = specialCases.Concat(groupSpecialCases.Where(p => !specialCases.ContainsKey(p.Key)))
                                               .ToDictionary(p => p.Key, p => p.Value);
                    
                    foreach (var framework in Frameworks.List()) {
                        var cell = table[framework, features[test]];
                        var specialCase = specialCases.GetValueOrDefault(framework.GetType());
                        if (specialCase != null && specialCase.Skip) {
                            cell.Text = "see comment";
                            cell.Comment = specialCase.Comment;
                            cell.State = FeatureState.Concern;
                            continue;
                        }

                        this.RunTestAndCollectResult(test, framework, cell);
                        if (specialCase != null)
                            cell.Comment = specialCase.Comment;
                    }
                }

                yield return table;
            }
        }

        private Feature ConvertToFeature(MethodInfo test) {
            return new Feature(test, this.GetDisplayName(test)) { Description = this.GetDescription(test) };
        }

        private void RunTestAndCollectResult(MethodInfo test, IFrameworkAdapter framework, FeatureCell cell) {
            var instance = Activator.CreateInstance(test.DeclaringType);
            try {
                test.Invoke(instance, new object[] {framework});
            }
            catch (Exception ex) {
                CollectFailure(cell, ex);
                return;
            }

            cell.Text = "supported";
            cell.State = FeatureState.Success;
        }

        private static void CollectFailure(FeatureCell cell, Exception exception) {
            cell.Text = "failed";
            cell.State = FeatureState.Failure;
            cell.Exception = ToUsefulException(exception);
        }

        private static Exception ToUsefulException(Exception exception) {
            var invocationException = exception as TargetInvocationException;
            if (invocationException != null)
                return ToUsefulException(invocationException.InnerException);
            
            return exception;
        }

        private string GetDescription(MemberInfo member) {
            var description = member.GetCustomAttributes<DescriptionAttribute>().Select(a => a.Description).SingleOrDefault();
            if (description.IsNullOrEmpty())
                return description;

            // replace all single new lines with spaces
            description = Regex.Replace(description, @"([^\r\n]|^)(?:\r\n|\r|\n)([^\r\n]|$)", "$1 $2");

            // collapse all spaces
            description = Regex.Replace(description, @" +", @" ");

            // remove all spaces at start/end of the line
            description = Regex.Replace(description, @"^ +| +$", "", RegexOptions.Multiline);
            return description;
        }

        private string GetDisplayName(MemberInfo member) {
            var displayNameAttribute = member.GetCustomAttributes<DisplayNameAttribute>().SingleOrDefault();
            if (displayNameAttribute == null)
                return member.Name;

            return displayNameAttribute.DisplayName;
        }

        private int GetDisplayOrder(MemberInfo member) {
            var displayOrderAttribute = member.GetCustomAttributes<DisplayOrderAttribute>().SingleOrDefault();
            if (displayOrderAttribute == null)
                return int.MaxValue;

            return displayOrderAttribute.Order;
        }

        private IDictionary<Type, SpecialCaseAttribute> GetSpecialCases(MemberInfo member) {
            return member.GetCustomAttributes<SpecialCaseAttribute>().ToDictionary(a => a.FrameworkType);
        }
    }
}