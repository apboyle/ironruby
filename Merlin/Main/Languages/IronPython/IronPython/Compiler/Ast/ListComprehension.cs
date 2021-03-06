/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;

using IronPython.Runtime;

#if !CLR2
using MSAst = System.Linq.Expressions;
#else
using MSAst = Microsoft.Scripting.Ast;
#endif

using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Compiler.Ast {
    using Ast = MSAst.Expression;

    public abstract class ListComprehensionIterator : Node {
        internal abstract MSAst.Expression Transform(AstGenerator ag, MSAst.Expression body);
    }

    public class ListComprehension : Expression {
        private readonly Expression _item;
        private readonly ListComprehensionIterator[] _iterators;

        public ListComprehension(Expression item, ListComprehensionIterator[] iterators) {
            _item = item;
            _iterators = iterators;
        }

        public Expression Item {
            get { return _item; }
        }

        public IList<ListComprehensionIterator> Iterators {
            get { return _iterators; }
        }

        internal override MSAst.Expression Transform(AstGenerator ag, Type type) {
            MSAst.ParameterExpression list = ag.GetTemporary("list_comprehension_list", typeof(List));

            // 1. Initialization code - create list and store it in the temp variable
            MSAst.Expression initialize =
                Ast.Assign(
                    list,
                    Ast.Call(
                        AstGenerator.GetHelperMethod("MakeList", Type.EmptyTypes) // method
                    )                    
                );

            // 2. Create body from _item:   list.Append(_item)
            MSAst.Expression body = ag.AddDebugInfo(
                Ast.Call(
                    AstGenerator.GetHelperMethod("ListAddForComprehension"),
                    list,
                    ag.TransformAsObject(_item)
                ),
                _item.Span
            );

            // 3. Transform all iterators in reverse order, building the true body:
            int current = _iterators.Length;
            while (current-- > 0) {
                ListComprehensionIterator iterator = _iterators[current];
                body = iterator.Transform(ag, body);
            }

            return Ast.Block(
                initialize,
                body,
                list                        // result
            );
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_item != null) {
                    _item.Walk(walker);
                }
                if (_iterators != null) {
                    foreach (ListComprehensionIterator lci in _iterators) {
                        lci.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }
    }
}
