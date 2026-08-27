using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using TaxMate.Infrastructure.Documents.Tax;
using TaxMate.Model.Documents.Tax;

namespace TemplateTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Testing S2b generator...");
                var s2bGen = new OpenXmlS2bDocumentGenerator();
                var s2bModel = new S2bDocumentModel {
                    BusinessName = "Test",
                    TaxCode = "123",
                    Address = "Test",
                    BusinessLocation = "Test",
                    Year = 2026,
                    Quarter = 1,
                    ExportDate = DateTime.Now,
                    RepresentativeName = "Test",
                    Groups = new List<S2bDocumentGroupModel> {
                        new S2bDocumentGroupModel {
                            BusinessCategoryName = "Cat",
                            TotalRevenue = 100,
                            VatRate = 1,
                            VatAmount = 1,
                            Lines = new List<S2bDocumentLineModel> {
                                new S2bDocumentLineModel {
                                    DocumentNumber = "1",
                                    DocumentDate = DateTime.Now,
                                    Description = "Desc",
                                    Amount = 100
                                }
                            }
                        }
                    }
                };
                await s2bGen.GenerateAsync(s2bModel);
                Console.WriteLine("S2b success!");
            }
            catch(Exception e) { Console.WriteLine("S2b failed: " + e.Message); }

            try {
                Console.WriteLine("Testing S2c generator...");
                var s2cGen = new OpenXmlS2cDocumentGenerator();
                var s2cModel = new S2cDocumentModel {
                    BusinessName = "Test", TaxCode = "123", Address = "Test", BusinessLocation = "Test", Year = 2026, Quarter = 1, ExportDate = DateTime.Now, RepresentativeName = "Test"
                };
                await s2cGen.GenerateAsync(s2cModel);
                Console.WriteLine("S2c success!");
            } catch(Exception e) { Console.WriteLine("S2c failed: " + e.Message); }

            try {
                Console.WriteLine("Testing S2d generator...");
                var s2dGen = new OpenXmlS2dDocumentGenerator();
                var s2dModel = new S2dDocumentModel {
                    BusinessName = "Test", TaxCode = "123", Address = "Test", Year = 2026, Quarter = 1, ExportDate = DateTime.Now, RepresentativeName = "Test",
                    Lines = new List<S2dDocumentLineModel>()
                };
                await s2dGen.GenerateAsync(s2dModel);
                Console.WriteLine("S2d success!");
            } catch(Exception e) { Console.WriteLine("S2d failed: " + e.Message); }

            try {
                Console.WriteLine("Testing S2e generator...");
                var s2eGen = new OpenXmlS2eDocumentGenerator();
                var s2eModel = new S2eDocumentModel {
                    BusinessName = "Test", TaxCode = "123", Address = "Test", Year = 2026, Quarter = 1, ExportDate = DateTime.Now, RepresentativeName = "Test",
                    Lines = new List<S2eDocumentLineModel>()
                };
                await s2eGen.GenerateAsync(s2eModel);
                Console.WriteLine("S2e success!");
            } catch(Exception e) { Console.WriteLine("S2e failed: " + e.Message); }
        }
    }
}
