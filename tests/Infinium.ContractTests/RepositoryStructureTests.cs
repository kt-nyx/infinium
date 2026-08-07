using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class RepositoryStructureTests
{
    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void AcceptedSliceZeroProjectSkeletonExists()
    {
        string[] projects =
        [
            "src/Infinium.Domain/Infinium.Domain.csproj",
            "src/Infinium.Application/Infinium.Application.csproj",
            "src/Infinium.Persistence/Infinium.Persistence.csproj",
            "src/Infinium.Mo2/Infinium.Mo2.csproj",
            "src/Infinium.Bethesda/Infinium.Bethesda.csproj",
            "src/Infinium.Analysis/Infinium.Analysis.csproj",
            "src/Infinium.OpenAI/Infinium.OpenAI.csproj",
            "src/Infinium.Coordinator/Infinium.Coordinator.csproj",
            "src/Infinium.Worker/Infinium.Worker.csproj",
            "src/Infinium.CredentialHelper/Infinium.CredentialHelper.csproj",
            "src/Infinium.Cli/Infinium.Cli.csproj",
            "tests/Infinium.UnitTests/Infinium.UnitTests.csproj",
            "tests/Infinium.ContractTests/Infinium.ContractTests.csproj",
            "tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj",
            "tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj",
        ];

        foreach (string project in projects)
        {
            Assert.IsTrue(
                File.Exists(TestRepository.PathFromRoot(project.Split('/'))),
                $"Required project '{project}' is missing.");
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void SliceOneContractDirectoriesAreImplementedAndEvaluationBoundaryIsCurrent()
    {
        string[] implementedReadmes =
        [
            "contracts/protobuf/README.md",
            "contracts/json-schema/README.md",
        ];
        foreach (string readme in implementedReadmes)
        {
            string content = TestRepository.Read(readme.Split('/'));
            StringAssert.Contains(content, "M1 Slice 1");
            StringAssert.Contains(content, "contract");
        }

        string[] reservedReadmes =
        [
            "test-data/synthetic/README.md",
            "test-data/manifests/README.md",
        ];

        foreach (string readme in reservedReadmes)
        {
            string content = TestRepository.Read(readme.Split('/'));
            StringAssert.Contains(content, "not implemented by Slice 0");
        }

        string evaluator = TestRepository.Read("tools", "evaluation", "README.md");
        StringAssert.Contains(evaluator, "Infinium.EvaluatorV2");
        StringAssert.Contains(evaluator, "frozen historical evaluator");
        StringAssert.Contains(evaluator, "not an active held-out workflow");
        StringAssert.Contains(evaluator, "Protocol `/5` is retired");
        StringAssert.Contains(evaluator, "unqualified with no implementation");
    }
}
