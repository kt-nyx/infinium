using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;


public sealed class AnalysisInputAdmissionException(string message, Exception innerException)
    : Exception(message, innerException);
