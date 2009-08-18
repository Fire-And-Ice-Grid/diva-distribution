using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.Tests
{
    public static class Constraints
    {
        //This is here because C# has a gap in the language, you can't infer type from a constructor
        public static PropertyCompareConstraint<T> PropertyCompareConstraint<T>(T expected)
        {
            return new PropertyCompareConstraint<T>(expected);
        }
    }

    public class PropertyCompareConstraint<T> : NUnit.Framework.Constraints.Constraint
    {
        private readonly object _expected;
        //the reason everywhere uses propertyNames.Reverse().ToArray() is because the stack is backwards of the order we want to display the properties in.
        private string failingPropertyName = string.Empty;
        private object failingExpected;
        private object failingActual;

        public PropertyCompareConstraint(T expected)
        {
            _expected = expected;
        }

        public override bool Matches(object actual)
        {
            return ObjectCompare(_expected, actual, new Stack<string>());
        }

        private bool ObjectCompare(object expected, object actual, Stack<string> propertyNames)
        {
            if (actual.GetType() != expected.GetType())
            {
                propertyNames.Push("GetType()");
                failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                propertyNames.Pop();
                failingActual = actual.GetType();
                failingExpected = expected.GetType();
                return false;
            }

            if(actual.GetType() == typeof(Color))
            {
                Color actualColor = (Color) actual;
                Color expectedColor = (Color) expected;
                if (actualColor.R != expectedColor.R)
                {
                    propertyNames.Push("R");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.R;
                    failingExpected = expectedColor.R;
                    return false;
                }
                if (actualColor.G != expectedColor.G)
                {
                    propertyNames.Push("G");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.G;
                    failingExpected = expectedColor.G;
                    return false;
                }
                if (actualColor.B != expectedColor.B)
                {
                    propertyNames.Push("B");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.B;
                    failingExpected = expectedColor.B;
                    return false;
                }
                if (actualColor.A != expectedColor.A)
                {
                    propertyNames.Push("A");
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualColor.A;
                    failingExpected = expectedColor.A;
                    return false;
                }
                return true;
            }

            //Skip static properties.  I had a nasty problem comparing colors because of all of the public static colors.
            PropertyInfo[] properties = expected.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (ignores.Contains(property.Name))
                    continue;

                object actualValue = property.GetValue(actual, null);
                object expectedValue = property.GetValue(expected, null);

                //If they are both null, they are equal
                if (actualValue == null && expectedValue == null)
                    continue;

                //If only one is null, then they aren't
                if (actualValue == null || expectedValue == null)
                {
                    propertyNames.Push(property.Name);
                    failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                    propertyNames.Pop();
                    failingActual = actualValue;
                    failingExpected = expectedValue;
                    return false;
                }

                IComparable comp = actualValue as IComparable;
                if (comp != null)
                {
                    if (comp.CompareTo(expectedValue) != 0)
                    {
                        propertyNames.Push(property.Name);
                        failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                        propertyNames.Pop();
                        failingActual = actualValue;
                        failingExpected = expectedValue;
                        return false;
                    }
                    continue;
                }

                IEnumerable arr = actualValue as IEnumerable;
                if (arr != null)
                {
                    List<object> actualList = arr.Cast<object>().ToList();
                    List<object> expectedList = ((IEnumerable)expectedValue).Cast<object>().ToList();
                    if (actualList.Count != expectedList.Count)
                    {
                        propertyNames.Push(property.Name);
                        propertyNames.Push("Count");
                        failingPropertyName = string.Join(".", propertyNames.Reverse().ToArray());
                        failingActual = actualList.Count;
                        failingExpected = expectedList.Count;
                        propertyNames.Pop();
                        propertyNames.Pop();
                    }
                    //Todo: A value-wise comparison of all of the values.
                    //Everything seems okay...
                    continue;
                }

                propertyNames.Push(property.Name);
                if (!ObjectCompare(expectedValue, actualValue, propertyNames))
                    return false;
                propertyNames.Pop();
            }

            return true;
        }

        public override void WriteDescriptionTo(MessageWriter writer)
        {
            writer.WriteExpectedValue(failingExpected);
        }

        public override void WriteActualValueTo(MessageWriter writer)
        {
            writer.WriteActualValue(failingActual);
            writer.WriteLine();
            writer.Write("  On Property: " + failingPropertyName);
        }

        //These notes assume the lambda: (x=>x.Parent.Value)
        //ignores should really contain like a fully dotted version of the property name, but I'm starting with small steps
        readonly List<string> ignores = new List<string>();
        public PropertyCompareConstraint<T> IgnoreProperty(Expression<Func<T, object>> func)
        {
            Expression express = func.Body;
            PullApartExpression(express);

            return this;
        }

        private void PullApartExpression(Expression express)
        {
            //This deals with any casts... like implicit casts to object.  Not all UnaryExpression are casts, but this is a first attempt.
            if (express is UnaryExpression)
                PullApartExpression(((UnaryExpression)express).Operand);
            if (express is MemberExpression)
            {
                //If the inside of the lambda is the access to x, we've hit the end of the chain.
                //   We should track by the fully scoped parameter name, but this is the first rev of doing this.
                if (((MemberExpression)express).Expression is ParameterExpression)
                {
                    ignores.Add(((MemberExpression)express).Member.Name);
                }
                else
                {
                    //Otherwise there could be more parameters inside...
                    PullApartExpression(((MemberExpression)express).Expression);
                }
            }
        }
    }

    [TestFixture]
    public class PropertyCompareConstraintTest
    {
        public class HasInt
        {
            public int TheValue { get; set; }
        }

        [Test]
        public void IntShouldMatch()
        {
            HasInt actual = new HasInt { TheValue = 5 };
            HasInt expected = new HasInt { TheValue = 5 };
            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.True);
        }

        [Test]
        public void IntShouldNotMatch()
        {
            HasInt actual = new HasInt { TheValue = 5 };
            HasInt expected = new HasInt { TheValue = 4 };
            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.False);
        }


        [Test]
        public void IntShouldIgnore()
        {
            HasInt actual = new HasInt { TheValue = 5 };
            HasInt expected = new HasInt { TheValue = 4 };
            var constraint = Constraints.PropertyCompareConstraint(expected).IgnoreProperty(x=>x.TheValue);

            Assert.That(constraint.Matches(actual), Is.True);
        }

        [Test]
        public void AssetShouldMatch()
        {
            UUID uuid1 = UUID.Random();
            AssetBase actual = new AssetBase(uuid1, "asset one");
            AssetBase expected = new AssetBase(uuid1, "asset one");

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.True);
        }

        [Test]
        public void AssetShouldNotMatch()
        {
            UUID uuid1 = UUID.Random();
            AssetBase actual = new AssetBase(uuid1, "asset one");
            AssetBase expected = new AssetBase(UUID.Random(), "asset one");

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.False);
        }

        [Test]
        public void AssetShouldNotMatch2()
        {
            UUID uuid1 = UUID.Random();
            AssetBase actual = new AssetBase(uuid1, "asset one");
            AssetBase expected = new AssetBase(uuid1, "asset two");

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.False);
        }

        [Test]
        public void TestColors()
        {
            Color actual = Color.Red;
            Color expected = Color.FromArgb(actual.A, actual.R, actual.G, actual.B);

            var constraint = Constraints.PropertyCompareConstraint(expected);

            Assert.That(constraint.Matches(actual), Is.True);
        }
    }
}