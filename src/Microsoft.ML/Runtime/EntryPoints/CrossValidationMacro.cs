// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.Runtime;
using Microsoft.ML.Runtime.CommandLine;
using Microsoft.ML.Runtime.Data;
using Microsoft.ML.Runtime.EntryPoints;
using Microsoft.ML.Runtime.Internal.Utilities;
using Newtonsoft.Json.Linq;

[assembly: LoadableClass(typeof(void), typeof(CrossValidationMacro), null, typeof(SignatureEntryPointModule), "CrossValidationMacro")]

namespace Microsoft.ML.Runtime.EntryPoints
{

    /// <summary>
    /// This macro entry point implements cross validation.
    /// </summary>
    public static class CrossValidationMacro
    {
        public sealed class SubGraphInput
        {
            [Argument(ArgumentType.Required, HelpText = "The data to be used for training", SortOrder = 1)]
            public Var<IDataView> Data;
        }

        public sealed class SubGraphOutput
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "The predictor model", SortOrder = 1)]
            public Var<IPredictorModel> PredictorModel;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The transform model", SortOrder = 2)]
            public Var<ITransformModel> TransformModel;
        }

        public sealed class Arguments
        {
            // This is the data used in the cross validation. It will be split into k folds
            // and a model will be trained and evaluated for each fold.
            [TlcModule.OptionalInput]
            [Argument(ArgumentType.Required, HelpText = "The data set", SortOrder = 1)]
            public IDataView Data;

            [TlcModule.OptionalInput]
            [Argument(ArgumentType.AtMostOnce, HelpText = "The transform model from the pipeline before this command. " +
                "It gets included in the Output.PredictorModel.", SortOrder = 2)]
            public ITransformModel TransformModel;

            // This is the subgraph that describes how to train a model for each fold. It should
            // accept one IDataView input and output one IPredictorModel output (see Inputs and Outputs).
            [Argument(ArgumentType.Required, HelpText = "The training subgraph", SortOrder = 3)]
            public JArray Nodes;

            // This is the subgraph input, that shows that the subgraph should only require one
            // IDataView as input and indicates the variable name (in the subgraph) for it.
            [Argument(ArgumentType.Required, HelpText = "The training subgraph inputs", SortOrder = 4)]
            public SubGraphInput Inputs = new SubGraphInput();

            // This is the subgraph output, that shows that the subgraph should produce one
            // IPredictorModel as output and indicates the variable name (in the subgraph) for it.
            [Argument(ArgumentType.Required, HelpText = "The training subgraph outputs", SortOrder = 5)]
            public SubGraphOutput Outputs = new SubGraphOutput();

            // For splitting the data into folds, this column is used for grouping rows and makes sure
            // that a group of rows is not split among folds.
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column to use for stratification", ShortName = "strat", SortOrder = 6)]
            public string StratificationColumn;

            // The number of folds to generate.
            [Argument(ArgumentType.AtMostOnce, HelpText = "Number of folds in k-fold cross-validation", ShortName = "k", SortOrder = 7)]
            public int NumFolds = 2;

            // REVIEW: suggest moving to subcomponents for evaluators, to allow for different parameters on the evaluators
            // (and the same for the TrainTest macro). I currently do not know how to do this, so this should be revisited in the future.
            [Argument(ArgumentType.Required, HelpText = "Specifies the trainer kind, which determines the evaluator to be used.", SortOrder = 8)]
            public MacroUtils.TrainerKinds Kind = MacroUtils.TrainerKinds.SignatureBinaryClassifierTrainer;

            [Argument(ArgumentType.AtMostOnce, HelpText = "Column to use for labels", ShortName = "lab", SortOrder = 9)]
            public string LabelColumn = DefaultColumnNames.Label;

            [Argument(ArgumentType.AtMostOnce, HelpText = "Column to use for example weight", ShortName = "weight", SortOrder = 10)]
            public Optional<string> WeightColumn = Optional<string>.Implicit(DefaultColumnNames.Weight);

            [Argument(ArgumentType.AtMostOnce, HelpText = "Column to use for grouping", ShortName = "group", SortOrder = 11)]
            public Optional<string> GroupColumn = Optional<string>.Implicit(DefaultColumnNames.GroupId);

            [Argument(ArgumentType.AtMostOnce, HelpText = "Name column name", ShortName = "name", SortOrder = 12)]
            public Optional<string> NameColumn = Optional<string>.Implicit(DefaultColumnNames.Name);
        }

        // REVIEW: This output would be much better as an array of CommonOutputs.ClassificationEvaluateOutput,
        // but that requires changes in the entry points infrastructure to support structs in the output classes.
        public sealed class Output
        {
            [TlcModule.Output(Desc = "The final model including the trained predictor model and the model from the transforms, " +
                "provided as the Input.TransformModel.", SortOrder = 1)]
            public IPredictorModel[] PredictorModel;

            [TlcModule.Output(Desc = "The final model including the trained predictor model and the model from the transforms, " +
                "provided as the Input.TransformModel.", SortOrder = 2)]
            public ITransformModel[] TransformModel;

            [TlcModule.Output(Desc = "Warning dataset", SortOrder = 3)]
            public IDataView Warnings;

            [TlcModule.Output(Desc = "Overall metrics dataset", SortOrder = 4)]
            public IDataView OverallMetrics;

            [TlcModule.Output(Desc = "Per instance metrics dataset", SortOrder = 5)]
            public IDataView PerInstanceMetrics;

            [TlcModule.Output(Desc = "Confusion matrix dataset", SortOrder = 6)]
            public IDataView ConfusionMatrix;
        }

        public sealed class CombineMetricsInput
        {
            [Argument(ArgumentType.Multiple, HelpText = "Overall metrics datasets", SortOrder = 1)]
            public IDataView[] OverallMetrics;

            [Argument(ArgumentType.Multiple, HelpText = "Per instance metrics datasets", SortOrder = 2)]
            public IDataView[] PerInstanceMetrics;

            [Argument(ArgumentType.Multiple, HelpText = "Confusion matrix datasets", SortOrder = 3)]
            public IDataView[] ConfusionMatrix;

            [Argument(ArgumentType.Multiple, HelpText = "Warning datasets", SortOrder = 4)]
            public IDataView[] Warnings;

            [Argument(ArgumentType.AtMostOnce, HelpText = "The label column name", ShortName = "Label", SortOrder = 6)]
            public string LabelColumn = DefaultColumnNames.Label;

            [Argument(ArgumentType.AtMostOnce, HelpText = "Column to use for example weight", ShortName = "weight", SortOrder = 7)]
            public Optional<string> WeightColumn = Optional<string>.Implicit(DefaultColumnNames.Weight);

            [Argument(ArgumentType.AtMostOnce, HelpText = "Column to use for grouping", ShortName = "group", SortOrder = 8)]
            public Optional<string> GroupColumn = Optional<string>.Implicit(DefaultColumnNames.GroupId);

            [Argument(ArgumentType.AtMostOnce, HelpText = "Name column name", ShortName = "name", SortOrder = 9)]
            public Optional<string> NameColumn = Optional<string>.Implicit(DefaultColumnNames.Name);

            [Argument(ArgumentType.Required, HelpText = "Specifies the trainer kind, which determines the evaluator to be used.", SortOrder = 5)]
            public MacroUtils.TrainerKinds Kind = MacroUtils.TrainerKinds.SignatureBinaryClassifierTrainer;
        }

        public sealed class CombinedOutput
        {
            [TlcModule.Output(Desc = "Warning dataset", SortOrder = 1)]
            public IDataView Warnings;

            [TlcModule.Output(Desc = "Overall metrics dataset", SortOrder = 2)]
            public IDataView OverallMetrics;

            [TlcModule.Output(Desc = "Per instance metrics dataset", SortOrder = 3)]
            public IDataView PerInstanceMetrics;

            [TlcModule.Output(Desc = "Confusion matrix dataset", SortOrder = 4)]
            public IDataView ConfusionMatrix;
        }

        [TlcModule.EntryPoint(Desc = "Cross validation for general learning", Name = "Models.CrossValidator")]
        public static CommonOutputs.MacroOutput<Output> CrossValidate(
            IHostEnvironment env,
            Arguments input,
            EntryPointNode node)
        {
            env.CheckValue(input, nameof(input));

            // This will be the final resulting list of nodes that is returned from the macro.
            var subGraphNodes = new List<EntryPointNode>();

            //the input transform model
            VariableBinding transformModelVarName = null;
            if (input.TransformModel != null)
                transformModelVarName = node.GetInputVariable(nameof(input.TransformModel));

            // Split the input data into folds.
            var exp = new Experiment(env);
            var cvSplit = new Legacy.Models.CrossValidatorDatasetSplitter();
            cvSplit.Data.VarName = node.GetInputVariable("Data").ToJson();
            cvSplit.NumFolds = input.NumFolds;
            cvSplit.StratificationColumn = input.StratificationColumn;
            var cvSplitOutput = exp.Add(cvSplit);
            subGraphNodes.AddRange(EntryPointNode.ValidateNodes(env, node.Context, exp.GetNodes(), node.Catalog));

            var predModelVars = new Var<IPredictorModel>[input.NumFolds];
            var transformModelVars = new Var<ITransformModel>[input.NumFolds];
            var inputTransformModelVars = new Var<IPredictorModel>[input.NumFolds];
            var warningsVars = new Var<IDataView>[input.NumFolds];
            var overallMetricsVars = new Var<IDataView>[input.NumFolds];
            var instanceMetricsVars = new Var<IDataView>[input.NumFolds];
            var confusionMatrixVars = new Var<IDataView>[input.NumFolds];
            // Instantiate the subgraph for each fold.
            for (int k = 0; k < input.NumFolds; k++)
            {
                // Parse the nodes in input.Nodes into a temporary run context.
                var context = new RunContext(env);
                var graph = EntryPointNode.ValidateNodes(env, context, input.Nodes, node.Catalog);

                // Rename all the variables such that they don't conflict with the ones in the outer run context.
                var mapping = new Dictionary<string, string>();
                foreach (var entryPointNode in graph)
                    entryPointNode.RenameAllVariables(mapping);

                // Instantiate a TrainTest entry point for this fold.
                var args = new TrainTestMacro.Arguments
                {
                    Nodes = new JArray(graph.Select(n => n.ToJson()).ToArray()),
                    TransformModel = null,
                    LabelColumn = input.LabelColumn,
                    GroupColumn = input.GroupColumn,
                    WeightColumn = input.WeightColumn,
                    NameColumn = input.NameColumn
                };

                if (transformModelVarName != null)
                    args.TransformModel = new Var<ITransformModel> { VarName = transformModelVarName.VariableName };

                args.Inputs.Data = new Var<IDataView>
                {
                    VarName = mapping[input.Inputs.Data.VarName]
                };

                if (input.Outputs.PredictorModel != null && mapping.ContainsKey(input.Outputs.PredictorModel.VarName))
                {
                    args.Outputs.PredictorModel = new Var<IPredictorModel>
                    {
                        VarName = mapping[input.Outputs.PredictorModel.VarName]
                    };
                }
                else
                    args.Outputs.PredictorModel = null;

                if (input.Outputs.TransformModel != null && mapping.ContainsKey(input.Outputs.TransformModel.VarName))
                {
                    args.Outputs.TransformModel = new Var<ITransformModel>
                    {
                        VarName = mapping[input.Outputs.TransformModel.VarName]
                    };
                }
                else
                    args.Outputs.TransformModel = null;

                // Set train/test trainer kind to match.
                args.Kind = input.Kind;

                // Set the input bindings for the TrainTest entry point.
                var inputBindingMap = new Dictionary<string, List<ParameterBinding>>();
                var inputMap = new Dictionary<ParameterBinding, VariableBinding>();
                var trainingData = new SimpleParameterBinding(nameof(args.TrainingData));
                inputBindingMap.Add(nameof(args.TrainingData), new List<ParameterBinding> { trainingData });
                inputMap.Add(trainingData, new ArrayIndexVariableBinding(cvSplitOutput.TrainData.VarName, k));
                var testingData = new SimpleParameterBinding(nameof(args.TestingData));
                inputBindingMap.Add(nameof(args.TestingData), new List<ParameterBinding> { testingData });
                inputMap.Add(testingData, new ArrayIndexVariableBinding(cvSplitOutput.TestData.VarName, k));
                var outputMap = new Dictionary<string, string>();
                var transformModelVar = new Var<ITransformModel>();
                var predModelVar = new Var<IPredictorModel>();
                if (input.Outputs.PredictorModel == null)
                {
                    outputMap.Add(nameof(TrainTestMacro.Output.TransformModel), transformModelVar.VarName);
                    transformModelVars[k] = transformModelVar;
                    Legacy.Transforms.ModelCombiner.Output modelCombineOutput = null;
                    if (transformModelVarName != null && transformModelVarName.VariableName != null)
                    {
                        var modelCombine = new Legacy.Transforms.ModelCombiner
                        {
                            Models = new ArrayVar<ITransformModel>(
                                new Var<ITransformModel>[] {
                                    new Var<ITransformModel> { VarName = transformModelVarName.VariableName },
                                    transformModelVar }
                                )
                        };

                        exp.Reset();
                        modelCombineOutput = exp.Add(modelCombine);
                        subGraphNodes.AddRange(EntryPointNode.ValidateNodes(env, node.Context, exp.GetNodes(), node.Catalog));
                        transformModelVars[k] = modelCombineOutput.OutputModel;
                    }
                }
                else
                {
                    outputMap.Add(nameof(TrainTestMacro.Output.PredictorModel), predModelVar.VarName);
                    predModelVars[k] = predModelVar;
                    Legacy.Transforms.TwoHeterogeneousModelCombiner.Output modelCombineOutput = null;
                    if (transformModelVarName != null && transformModelVarName.VariableName != null)
                    {
                        var modelCombine = new Legacy.Transforms.TwoHeterogeneousModelCombiner
                        {
                            TransformModel = { VarName = transformModelVarName.VariableName },
                            PredictorModel = predModelVar
                        };

                        exp.Reset();
                        modelCombineOutput = exp.Add(modelCombine);
                        subGraphNodes.AddRange(EntryPointNode.ValidateNodes(env, node.Context, exp.GetNodes(), node.Catalog));
                        predModelVars[k] = modelCombineOutput.PredictorModel;
                    }
                }

                var warningVar = new Var<IDataView>();
                outputMap.Add(nameof(TrainTestMacro.Output.Warnings), warningVar.VarName);
                warningsVars[k] = warningVar;
                var overallMetric = new Var<IDataView>();
                outputMap.Add(nameof(TrainTestMacro.Output.OverallMetrics), overallMetric.VarName);
                overallMetricsVars[k] = overallMetric;
                var instanceMetric = new Var<IDataView>();
                outputMap.Add(nameof(TrainTestMacro.Output.PerInstanceMetrics), instanceMetric.VarName);
                instanceMetricsVars[k] = instanceMetric;
                var confusionMatrix = new Var<IDataView>();
                outputMap.Add(nameof(TrainTestMacro.Output.ConfusionMatrix), confusionMatrix.VarName);
                confusionMatrixVars[k] = confusionMatrix;
                const string trainTestEvaluatorMacroEntryPoint = "Models.TrainTestEvaluator";
                subGraphNodes.Add(EntryPointNode.Create(env, trainTestEvaluatorMacroEntryPoint, args, node.Catalog, node.Context, inputBindingMap, inputMap, outputMap));
            }

            exp.Reset();

            // Convert predictors from all folds into an array of predictors.

            if (input.Outputs.PredictorModel == null)
            {
                var outModels = new Legacy.Data.TransformModelArrayConverter
                {
                    TransformModel = new ArrayVar<ITransformModel>(transformModelVars)
                };
                var outModelsOutput = new Legacy.Data.TransformModelArrayConverter.Output();
                outModelsOutput.OutputModel.VarName = node.GetOutputVariableName(nameof(Output.TransformModel));
                exp.Add(outModels, outModelsOutput);
            }
            else
            {
                var outModels = new Legacy.Data.PredictorModelArrayConverter
                {
                    Model = new ArrayVar<IPredictorModel>(predModelVars)
                };
                var outModelsOutput = new Legacy.Data.PredictorModelArrayConverter.Output();
                outModelsOutput.OutputModel.VarName = node.GetOutputVariableName(nameof(Output.PredictorModel));
                exp.Add(outModels, outModelsOutput);
            }

            // Convert warnings data views from all folds into an array of data views.
            var warnings = new Legacy.Data.IDataViewArrayConverter
            {
                Data = new ArrayVar<IDataView>(warningsVars)
            };
            var warningsOutput = new Legacy.Data.IDataViewArrayConverter.Output();
            exp.Add(warnings, warningsOutput);

            // Convert overall metrics data views from all folds into an array of data views.
            var overallMetrics = new Legacy.Data.IDataViewArrayConverter
            {
                Data = new ArrayVar<IDataView>(overallMetricsVars)
            };
            var overallMetricsOutput = new Legacy.Data.IDataViewArrayConverter.Output();
            exp.Add(overallMetrics, overallMetricsOutput);

            // Convert per instance data views from all folds into an array of data views.
            var instanceMetrics = new Legacy.Data.IDataViewArrayConverter
            {
                Data = new ArrayVar<IDataView>(instanceMetricsVars)
            };
            var instanceMetricsOutput = new Legacy.Data.IDataViewArrayConverter.Output();
            exp.Add(instanceMetrics, instanceMetricsOutput);

            Legacy.Data.IDataViewArrayConverter.Output confusionMatricesOutput = null;
            if (input.Kind == MacroUtils.TrainerKinds.SignatureBinaryClassifierTrainer ||
                input.Kind == MacroUtils.TrainerKinds.SignatureMultiClassClassifierTrainer)
            {
                // Convert confusion matrix data views from all folds into an array of data views.
                var confusionMatrices = new Legacy.Data.IDataViewArrayConverter
                {
                    Data = new ArrayVar<IDataView>(confusionMatrixVars)
                };
                confusionMatricesOutput = new Legacy.Data.IDataViewArrayConverter.Output();
                exp.Add(confusionMatrices, confusionMatricesOutput);
            }

            var combineArgs = new CombineMetricsInput();
            combineArgs.Kind = input.Kind;
            combineArgs.LabelColumn = input.LabelColumn;
            combineArgs.WeightColumn = input.WeightColumn;
            combineArgs.GroupColumn = input.GroupColumn;
            combineArgs.NameColumn = input.NameColumn;

            // Set the input bindings for the CombineMetrics entry point.
            var combineInputBindingMap = new Dictionary<string, List<ParameterBinding>>();
            var combineInputMap = new Dictionary<ParameterBinding, VariableBinding>();

            var warningsArray = new SimpleParameterBinding(nameof(combineArgs.Warnings));
            combineInputBindingMap.Add(nameof(combineArgs.Warnings), new List<ParameterBinding> { warningsArray });
            combineInputMap.Add(warningsArray, new SimpleVariableBinding(warningsOutput.OutputData.VarName));
            var overallArray = new SimpleParameterBinding(nameof(combineArgs.OverallMetrics));
            combineInputBindingMap.Add(nameof(combineArgs.OverallMetrics), new List<ParameterBinding> { overallArray });
            combineInputMap.Add(overallArray, new SimpleVariableBinding(overallMetricsOutput.OutputData.VarName));
            var combinePerInstArray = new SimpleParameterBinding(nameof(combineArgs.PerInstanceMetrics));
            combineInputBindingMap.Add(nameof(combineArgs.PerInstanceMetrics), new List<ParameterBinding> { combinePerInstArray });
            combineInputMap.Add(combinePerInstArray, new SimpleVariableBinding(instanceMetricsOutput.OutputData.VarName));
            if (confusionMatricesOutput != null)
            {
                var combineConfArray = new SimpleParameterBinding(nameof(combineArgs.ConfusionMatrix));
                combineInputBindingMap.Add(nameof(combineArgs.ConfusionMatrix), new List<ParameterBinding> { combineConfArray });
                combineInputMap.Add(combineConfArray, new SimpleVariableBinding(confusionMatricesOutput.OutputData.VarName));
            }

            var combineOutputMap = new Dictionary<string, string>();
            var combineWarningVar = new Var<IDataView>();
            combineWarningVar.VarName = node.GetOutputVariableName(nameof(Output.Warnings));
            combineOutputMap.Add(nameof(Output.Warnings), combineWarningVar.VarName);
            var combineOverallMetric = new Var<IDataView>();
            combineOverallMetric.VarName = node.GetOutputVariableName(nameof(Output.OverallMetrics));
            combineOutputMap.Add(nameof(Output.OverallMetrics), combineOverallMetric.VarName);
            var combineInstanceMetric = new Var<IDataView>();
            combineInstanceMetric.VarName = node.GetOutputVariableName(nameof(Output.PerInstanceMetrics));
            combineOutputMap.Add(nameof(Output.PerInstanceMetrics), combineInstanceMetric.VarName);
            if (confusionMatricesOutput != null)
            {
                var combineConfusionMatrix = new Var<IDataView>();
                combineConfusionMatrix.VarName = node.GetOutputVariableName(nameof(Output.ConfusionMatrix));
                combineOutputMap.Add(nameof(TrainTestMacro.Output.ConfusionMatrix), combineConfusionMatrix.VarName);
            }
            subGraphNodes.AddRange(EntryPointNode.ValidateNodes(env, node.Context, exp.GetNodes(), node.Catalog));
            subGraphNodes.Add(EntryPointNode.Create(env, "Models.CrossValidationResultsCombiner", combineArgs, node.Catalog, node.Context, combineInputBindingMap, combineInputMap, combineOutputMap));
            return new CommonOutputs.MacroOutput<Output>() { Nodes = subGraphNodes };
        }

        [TlcModule.EntryPoint(Desc = "Combine the metric data views returned from cross validation.", Name = "Models.CrossValidationResultsCombiner")]
        public static CombinedOutput CombineMetrics(IHostEnvironment env, CombineMetricsInput input)
        {
            var eval = GetEvaluator(env, input.Kind);

            var perInst = EvaluateUtils.ConcatenatePerInstanceDataViews(env, eval, true, true, input.PerInstanceMetrics.Select(
                idv => new RoleMappedData(idv, opt: true,
                    RoleMappedSchema.ColumnRole.Label.Bind(input.LabelColumn),
                    RoleMappedSchema.ColumnRole.Weight.Bind(input.WeightColumn.Value),
                    RoleMappedSchema.ColumnRole.Group.Bind(input.GroupColumn),
                    RoleMappedSchema.ColumnRole.Name.Bind(input.NameColumn.Value))).ToArray(),
                out var variableSizeVectorColumnNames);

            var warnings = input.Warnings != null ? new List<IDataView>(input.Warnings) : new List<IDataView>();
            if (variableSizeVectorColumnNames.Length > 0)
            {
                var dvBldr = new ArrayDataViewBuilder(env);
                var warn = $"Detected columns of variable length: {string.Join(", ", variableSizeVectorColumnNames)}." +
                    $" Consider setting collateMetrics- for meaningful per-Folds results.";
                dvBldr.AddColumn(MetricKinds.ColumnNames.WarningText, TextType.Instance, new DvText(warn));
                warnings.Add(dvBldr.GetDataView());
            }

            env.Assert(Utils.Size(perInst) == 1);

            var overall = eval.GetOverallResults(input.OverallMetrics);
            overall = EvaluateUtils.CombineFoldMetricsDataViews(env, overall, input.OverallMetrics.Length);

            IDataView conf = null;
            if (Utils.Size(input.ConfusionMatrix) > 0)
            {
                EvaluateUtils.ReconcileSlotNames<double>(env, input.ConfusionMatrix, MetricKinds.ColumnNames.Count, NumberType.R8);

                for (int i = 0; i < input.ConfusionMatrix.Length; i++)
                {
                    var idv = input.ConfusionMatrix[i];
                    // Find the old Count column and drop it.
                    for (int col = 0; col < idv.Schema.ColumnCount; col++)
                    {
                        if (idv.Schema.IsHidden(col) &&
                            idv.Schema.GetColumnName(col).Equals(MetricKinds.ColumnNames.Count))
                        {
                            input.ConfusionMatrix[i] = new ChooseColumnsByIndexTransform(env,
                                new ChooseColumnsByIndexTransform.Arguments() { Drop = true, Index = new[] { col } }, idv);
                            break;
                        }
                    }
                }

                conf = EvaluateUtils.ConcatenateOverallMetrics(env, input.ConfusionMatrix);
            }

            var warningsIdv = warnings.Count > 0 ? AppendRowsDataView.Create(env, warnings[0].Schema, warnings.ToArray()) : null;

            return new CombinedOutput()
            {
                PerInstanceMetrics = perInst[0],
                OverallMetrics = overall,
                ConfusionMatrix = conf,
                Warnings = warningsIdv
            };
        }

        private static IMamlEvaluator GetEvaluator(IHostEnvironment env, MacroUtils.TrainerKinds kind)
        {
            switch (kind)
            {
                case MacroUtils.TrainerKinds.SignatureBinaryClassifierTrainer:
                    return new BinaryClassifierMamlEvaluator(env, new BinaryClassifierMamlEvaluator.Arguments());
                case MacroUtils.TrainerKinds.SignatureMultiClassClassifierTrainer:
                    return new MultiClassMamlEvaluator(env, new MultiClassMamlEvaluator.Arguments());
                case MacroUtils.TrainerKinds.SignatureRegressorTrainer:
                    return new RegressionMamlEvaluator(env, new RegressionMamlEvaluator.Arguments());
                case MacroUtils.TrainerKinds.SignatureRankerTrainer:
                    return new RankerMamlEvaluator(env, new RankerMamlEvaluator.Arguments());
                case MacroUtils.TrainerKinds.SignatureAnomalyDetectorTrainer:
                    return new AnomalyDetectionMamlEvaluator(env, new AnomalyDetectionMamlEvaluator.Arguments());
                case MacroUtils.TrainerKinds.SignatureClusteringTrainer:
                    return new ClusteringMamlEvaluator(env, new ClusteringMamlEvaluator.Arguments());
                case MacroUtils.TrainerKinds.SignatureMultiOutputRegressorTrainer:
                    return new MultiOutputRegressionMamlEvaluator(env, new MultiOutputRegressionMamlEvaluator.Arguments());
                default:
                    throw env.ExceptParam(nameof(kind), $"Trainer kind {kind} does not have an evaluator");
            }
        }
    }
}
