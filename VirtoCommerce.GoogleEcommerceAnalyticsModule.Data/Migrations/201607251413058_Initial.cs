namespace VirtoCommerce.GoogleEcommerceAnalyticsModule.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.GoogleEcommerceAnalitics",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        StoreId = c.String(maxLength: 128),
                        GoogleTagManagerId = c.String(maxLength: 128),
                        IsActive = c.Boolean(nullable: false),
                        CreatedDate = c.DateTime(nullable: false),
                        ModifiedDate = c.DateTime(),
                        CreatedBy = c.String(),
                        ModifiedBy = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.GoogleEcommerceAnalitics");
        }
    }
}
