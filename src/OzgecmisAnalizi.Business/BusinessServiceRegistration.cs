using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OzgecmisAnalizi.Business.Abstractions;
using OzgecmisAnalizi.Business.Ai;
using OzgecmisAnalizi.Business.Analysis;
using OzgecmisAnalizi.Business.Jobs;
using OzgecmisAnalizi.Business.Options;
using OzgecmisAnalizi.Business.Parsing;
using OzgecmisAnalizi.Business.Pdf;
using OzgecmisAnalizi.Business.Services;
using OzgecmisAnalizi.Core.Security;
using OzgecmisAnalizi.Entities.Concrete;
using QuestPDF.Infrastructure;

namespace OzgecmisAnalizi.Business;

public static class BusinessServiceRegistration
{
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration configuration)
    {
        // QuestPDF Community lisansı: yıllık geliri 1M USD altındaki kişi/kurumlar için ücretsiz.
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<SalaryOptions>(configuration.GetSection(SalaryOptions.SectionName));
        services.Configure<JobSearchOptions>(configuration.GetSection(JobSearchOptions.SectionName));

        // Ayrıştırma ve analiz
        services.AddSingleton<IResumeTextExtractor, ResumeTextExtractor>();
        services.AddSingleton<IResumeTextParser, ResumeTextParser>();
        services.AddSingleton<ISkillCatalog, SkillCatalog>();
        services.AddSingleton<ISalaryEstimator, SalaryEstimator>();
        services.AddSingleton<IResumeAnalyzer, ResumeAnalyzer>();
        services.AddSingleton<IResumePdfRenderer, ResumePdfRenderer>();
        services.AddSingleton<IAnalysisReportRenderer, AnalysisReportRenderer>();

        // Yapay zeka: appsettings "Ai:Provider" değerine göre seçilir.
        services.AddHttpClient<OpenAiCompatibleProvider>((sp, c) => c.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<AiOptions>>().Value.TimeoutSeconds));
        services.AddHttpClient<GeminiProvider>((sp, c) => c.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<AiOptions>>().Value.TimeoutSeconds));
        services.AddTransient<IAiProvider>(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value.Provider switch
        {
            AiProviderKind.OpenAICompatible => sp.GetRequiredService<OpenAiCompatibleProvider>(),
            AiProviderKind.Gemini => sp.GetRequiredService<GeminiProvider>(),
            _ => new NullAiProvider()
        });

        // İş arama
        services.AddHttpClient<JoobleProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<JSearchProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddTransient<IJobProvider>(sp => sp.GetRequiredService<JoobleProvider>());
        services.AddTransient<IJobProvider>(sp => sp.GetRequiredService<JSearchProvider>());
        services.AddSingleton<JobSiteLinkBuilder>();

        // Uygulama servisleri
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IResumeService, ResumeService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IProfessionService, ProfessionService>();
        services.AddScoped<IJobSearchService, JobSearchService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ICoverLetterService, CoverLetterService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
