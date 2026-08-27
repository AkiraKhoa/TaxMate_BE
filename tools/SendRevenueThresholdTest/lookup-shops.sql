SELECT bp."Id", bp."BusinessName", bp."IsActive",
       COALESCE(SUM(t."TotalAmount"), 0) AS revenue
FROM "BusinessProfiles" bp
LEFT JOIN "Transactions" t
  ON t."BusinessId" = bp."Id"
 AND t."TransactionType" = 'Sale'
 AND t."Status" = 'Completed'
 AND t."TransactionDate" >= TIMESTAMP '2025-10-01'
 AND t."TransactionDate" < TIMESTAMP '2026-10-01'
WHERE bp."OwnerId" = '62c43a42-af08-49ad-8cde-03b60ca002d9'
GROUP BY bp."Id", bp."BusinessName", bp."IsActive"
ORDER BY bp."BusinessName";
