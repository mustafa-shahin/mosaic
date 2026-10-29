using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mosaic.Domain.Settings;

namespace Mosaic.Persistence.Configurations.Settings;

internal sealed class InstanceSettingsConfiguration : IEntityTypeConfiguration<InstanceSettings>
{
    public void Configure(EntityTypeBuilder<InstanceSettings> builder)
    {
        builder.HasKey(settings => settings.Id);

        // never generated: with an identity column, a claim that forgot its id would still get 1 on a new database
        builder.Property(settings => settings.Id).ValueGeneratedNever();

        builder.ToTable(table => table.HasCheckConstraint("ck_instance_settings_single_row", $"id = {InstanceSettings.SingleRowId}"));
    }
}
