using Microsoft.Extensions.Caching.Memory;

namespace M03_Opus.Infrastructure.Caching;

/// <summary>
/// Cache danh mục ít thay đổi trong 10 phút và xóa cache khi cập nhật (Tình huống 26 &amp; M2).
/// </summary>
public sealed class CatalogCache(IMemoryCache cache)
{
    public const string FacultiesKey = "catalog:faculties";
    public const string MajorsKey = "catalog:majors";
    public const string CohortsKey = "catalog:cohorts";
    public const string ClassesKey = "catalog:classes";
    public const string SemestersKey = "catalog:semesters";
    public const string CoursesKey = "catalog:courses";
    public const string LecturersKey = "catalog:lecturers";
    public const string PrerequisitesKey = "catalog:prerequisites";
    public const string CurriculaKey = "catalog:curricula";
    public const string CurriculumCoursesKey = "catalog:curriculum-courses";

    private static readonly string[] AllKeys =
    [
        FacultiesKey, MajorsKey, CohortsKey, ClassesKey, SemestersKey,
        CoursesKey, LecturersKey, PrerequisitesKey, CurriculaKey, CurriculumCoursesKey
    ];

    public async Task<IReadOnlyList<T>> GetOrCreateAsync<T>(
        string key,
        Func<Task<List<T>>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (cache.TryGetValue(key, out IReadOnlyList<T>? cached) && cached is not null)
        {
            return cached;
        }

        var fresh = await factory().ConfigureAwait(false);
        cache.Set<IReadOnlyList<T>>(key, fresh, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });
        return fresh;
    }

    public void InvalidateAll()
    {
        foreach (var key in AllKeys)
        {
            cache.Remove(key);
        }
    }
}
