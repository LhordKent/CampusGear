"""Rebuild the maintained-source index from the checked-out application. No app startup or SQL."""
from pathlib import Path
import os,re
root=Path(__file__).resolve().parents[1]
base=root/"CampusGear"
tick=chr(96)
def code(s): return tick+s+tick
def link(p): return "["+str(p.relative_to(root)).replace("\\","/")+"](../"+str(p.relative_to(root)).replace("\\","/")+")"
def collect():
    found=[]
    for folder,dirs,files in os.walk(base):
        dirs[:]=[d for d in dirs if d not in {"bin","obj","lib","Migrations","Frames"}]
        for name in files:
            p=Path(folder)/name
            if p.suffix in {".cs",".cshtml",".js",".css"} and name!="figma-generated.css":
                found.append(p)
    return sorted(found)
files=collect()
sources={p:p.read_text(encoding="utf-8-sig") for p in files}
types={}
declaration=r"(?m)^\s*(?:(?:public|private|internal|protected|sealed|abstract|static|partial|readonly)\s+)*(?:class|interface|record(?:\s+struct)?|enum)\s+(\w+)"
for p,s in sources.items():
    for name in re.findall(declaration,s):
        types.setdefault(name,[]).append(p)
purpose={
"AccountService":"Password/status decisions; registration/approval handoff; administrator and password-reset session workflows. Change authentication decisions here.",
"UserAdministrationService":"Approval, roles/access/eligibility, account creation and history-preserving deletion; global/profile locks and audits.",
"BorrowerService":"Own-profile privacy, catalog grouping, quantity allocation, deterministic request IDs, retry/compensation, borrower reports/calendar.",
"ReservationService":"Authoritative per-unit state machine, eligibility and permissions, overlap/release/return/expiry rules and audit timing.",
"EmailChallengeService":"Protected one-time challenges: serializable issuance, attempt/expiry/consumption rules and send-after-commit.",
"CategoryService":"Category validation, versions, category/item locks, history-sensitive delete/deactivate and audits.",
"InventoryService":"Equipment rules, category revalidation, upload/database compensation, maintenance creation and audit orchestration.",
"MaintenanceService":"Open/update/close repair rules, loan/case checks, versions, repair hold and audit decisions.",
"EquipmentAvailabilityService":"Shared item-state precedence and picker data; labels/icons remain in WebApp.",
"DirectoryService":"Read-only category and borrower directory DTOs with SQL-backed filtering/paging.",
"CustodianService":"Desk queues/details, staff reports/calendar; all mutations remain ReservationService workflows.",
"DashboardService":"Independent administrator/custodian counts and chart periods; preserve differing meanings.",
"CurrentAccountService":"Identity lookup converted into name/initial strings for the header component.",
"EquipmentImageStore":"Validate size and leading image bytes; generate trusted file names, store/delete local equipment photos.",
"SmtpEmailSender":"Configured SMTP transport, timeout/cancellation, message delivery; does not decide account workflow.",
"DevelopmentEmailSender":"Bounded in-memory mailbox for local diagnostics/tests; sends no real mail.",
"DevelopmentMirrorEmailSender":"Development SMTP plus local mailbox mirror; mirror presence alone does not prove delivery.",
"ReservationExpiryWorker":"Scoped, bounded background expiry batches, minute cadence, retries and shutdown handling.",
"ApplicationDbContext":"Schema mappings, relationships, indexes/check constraints, rowversions and Identity model. Change schema only through a separate approved migration task.",
"DbInitializer":"Startup infrastructure: existing migrations, roles, initial catalog and explicit operator bootstrap; not a feature service.",
"DemoData":"Explicit Development-only synthetic records and removal; never creates/replaces accounts.",
"AccountUserStore":"Built-in Identity EF store with relational duplicate-error translation; retains Identity password/role/stamp behavior.",
"UnitOfWork":"One scoped context, save translation and async transaction adapter. Services choose commit boundaries.",
"WorkflowLockManager":"Transaction-owned SQL sp_getapplock, ten-second timeout and persistence busy result.",
"OperationResult":"Outcome/field-error/reload contracts, server actor metadata, version checks and audit construction.",
"AccountFlowStore":"Web session transport preserving exact keys, missing values and empty markers.",
"WorkspaceIdentityViewComponent":"Server principal to current-account service to safe Razor header display.",
"DevelopmentConfiguration":"Development fallback/User Secrets/environment/command-line loading precedence; no credentials in diagnostics.",
"FigmaUi":"Live navigation and reference-node mapping; display only.",
"AuthResponses":"Cookie-auth HTTP navigation versus background-request classification.",
"AccountAntiforgeryRecoveryFilter":"Refresh rejected account forms after stale/missing antiforgery state without executing the action.",
"ChartGeometry":"SVG chart geometry and scale calculation; no database queries.",
"ItemStatus":"Display-only status label mapping; state calculations live in EquipmentAvailabilityService.",
"EquipmentPicker":"Presentation wrapper for already-materialized picker choices; no persistence.",
"Program":"Composition root, middleware/cookies/session/rates, Data/Services registration and explicit startup infrastructure exceptions.",
"theme.js":"Persist/apply theme preference and synchronize theme controls.",
"toast.js":"Accessible transient feedback presentation and dismissal.",
"account.js":"Account-form interactions and client validation affordances; server remains authoritative.",
"borrower-catalog.js":"Catalog search/filter/quantity modal and availability requests.",
"calendar.js":"Calendar filtering/navigation and chosen-day presentation.",
"charts.js":"Chart presentation/interaction; receives server data.",
"equipment-picker.js":"Filter/select preloaded equipment choices and update hidden selection fields.",
"figma-demo.js":"Reference-only /demo interactions; does not implement live workflows.",
"figma-live.js":"Live form/dialog/navigation interactions mapped to saved design controls.",
"home-motion.js":"Borrower home visual motion, respecting browser preferences.",
"live-search.js":"Debounced URL-driven server search and partial-result swapping.",
"modal-forms.js":"Progressive fetch form submission and field feedback for maintenance dialogs.",
"motion.js":"Shared progressive motion and reduced-motion behavior.",
"site.js":"Template site script placeholder; inspect usages before adding behavior."
}
entity_tables={"ApplicationUser":"AspNetUsers","BorrowerProfile":"BorrowerProfiles","EquipmentCategory":"EquipmentCategories","EquipmentItem":"EquipmentItems","Reservation":"Reservations","Loan":"Loans","MaintenanceCase":"MaintenanceCases","AuditEvent":"AuditEvents","EmailChallenge":"EmailChallenges"}
def feature(p):
    name=str(p)
    for words,tests in [
        (("Account","User","Flow","Identity","Password","DevelopmentConfiguration"),"AccountRegistrationTests, AccountRoleManagementTests, AccountFlowIsolationTests, PasswordResetTests, DevelopmentConfigurationTests"),
        (("Borrower","Reservation","Loan"),"ReservationServiceSqlTests, ReservationSubmissionTests, BorrowerOrchestrationTests, RefactorCharacterizationTests, BorrowerProfilesTests"),
        (("Inventory","Equipment","Image"),"InventoryCompensationTests, EquipmentImageStoreTests, RefactorCharacterizationTests, ReleaseAvailabilityTests"),
        (("Maintenance",),"Round3BehaviourTests, RefactorCharacterizationTests"),
        (("Category","Categories"),"RolePermissionTests, RefactorCharacterizationTests"),
        (("Dashboard","Workspace","Directory","Custodian","History","Calendar"),"RolePermissionTests, HistoryReportTests, ReleaseAvailabilityTests")]:
        if any(w in name for w in words):return tests
    return "ArchitectureBoundaryTests, RolePermissionTests"
def role(p,s):
    stem=p.stem
    if p.name in purpose:return purpose[p.name]
    if stem in purpose:return purpose[stem]
    if p.suffix==".cshtml":
        model=re.search(r"@model\s+([^\r\n]+)",s)
        return "Razor "+("partial" if p.name.startswith("_") else "screen/template")+"; "+("receives "+code(model[1].strip())+"; " if model else "")+"edit markup/display here, workflow rules in the linked feature service."
    if p.suffix==".css":
        return "Presentation stylesheet for "+stem.replace("-"," ")+"; edit selectors/tokens here, verify responsive layout and focus/reduced-motion states."
    if stem.endswith("Controller"):
        return "HTTP adapter: route/authorization, binding, service outcomes to existing views/JSON/redirects. Change transport behavior here."
    if stem.startswith("I") and re.search(r"\binterface\b",s):
        return "Dependency-injection contract for "+stem[1:]+"; inputs/results are the boundary. Implementations and callers are linked below."
    if stem.endswith("Repository"):
        return "Data query/persistence implementation for "+stem[:-10]+"; keep filtering, ordering and paging in SQL. Does not commit independently."
    if stem in entity_tables:
        return "Tracked persistence entity for "+entity_tables[stem]+"; never pass to a Razor view. DbContext owns mapping."
    if "QueryModels" in p.parts:
        return "Materialized Data query/filter/projection contracts; only internal service reads may carry tracked entities. UI records contain scalars."
    if "ServiceModels" in p.parts:
        return "Plain command/result/display contracts; explicit controller/service mapping preserves names and responses."
    if "IntegrationTests" in str(p):
        return "Isolated verification fixture" if "Fixture" in stem else "Behavior/architecture regression checks; assertions listed below are executable contracts."
    if stem=="DependencyInjection":
        return "Shared scoped registration extension; application and SQL fixtures use the same persistence/service registrations."
    if "Constants" in p.parts:
        return "Shared enum/limit vocabulary; preserve persisted numeric values when adding states."
    if "Models" in p.parts:
        return "Web form/display model or formatting helper; annotated validation belongs here, database/business work belongs in Services."
    if "Pages" in p.parts:
        return "Razor Pages shell/reference/error infrastructure; generated Figma states are cataloged separately."
    return "Application-owned "+p.suffix.lstrip(".")+" source; use the linked symbols/callers to locate its feature."
out=["# Maintained source catalog","","Generated by [update-code-map.py](../scripts/update-code-map.py). Regenerate after file/symbol changes; the architecture/documentation tests enforce coverage. Links are relative and symbols are searchable. This is a navigation aid, not a replacement for reading a method before changing it.","","Read [the beginner guide](CODEBASE_CHEATSHEET.md) and [feature traces](FEATURE_TRACES.md) first. In each group, the start column identifies the edit point. Tests named in the last column live in [IntegrationTests](../CampusGear/CampusGear.IntegrationTests). No listed UI file may query SQL; Data contracts can return tracked entities only to internal service code.",""]
groups={}
for p in files:
    rel=p.relative_to(base)
    group="/".join(rel.parts[:2]) if len(rel.parts)>2 else str(rel.parts[0])
    groups.setdefault(group,[]).append(p)
for group,paths in groups.items():
    out+=["## "+group,"","| File / start here | Responsibility | Symbols, routes, inputs/results | Calls / called by | Resources, failures and checks |","|---|---|---|---|---|"]
    for p in paths:
        s=sources[p]
        symbols=list(dict.fromkeys(re.findall(declaration,s)+re.findall(r"(?m)^\s*(?:public|private|protected|internal)\s+(?:(?:static|async|override|sealed|virtual|partial)\s+)*[\w<>\[\]?,.]+\s+(\w+)\s*\(",s)))
        routes=[verb.upper()+" "+url for verb,url in re.findall(r'Http(Get|Post)\("([^"]+)"',s)]
        extras=[]
        if p.suffix==".js":
            extras=list(dict.fromkeys(re.findall(r"data-[a-z][a-z0-9-]+",s)))[:7]
        if p.suffix==".css":
            extras=list(dict.fromkeys(re.findall(r"\.([a-z][\w-]+)",s)))[:7]
        if p.suffix==".cshtml":
            extras=list(dict.fromkeys(re.findall(r'(?:action|src|href)="([^"@][^"]*)"',s)))[:7]
        symbols_text=", ".join(code(x) for x in symbols+routes+extras) or "Template/runtime hooks shown in source."
        refs=[]
        for name in dict.fromkeys(re.findall(r"\b[A-Z]\w+",s)):
            if name in types:
                for target in types[name]:
                    if target!=p and "IntegrationTests" not in str(target) and not name.endswith("Input"):
                        refs.append((name,target))
        refs=list(dict.fromkeys(refs))[:10]
        calls=", ".join("["+n+"](../"+str(t.relative_to(root)).replace("\\","/")+")" for n,t in refs)
        names=[name for name,ps in types.items() if p in ps and (name.endswith("Service") or name.endswith("Repository") or name.endswith("Controller") or name.startswith("I"))]
        callers=[q.stem for q,src in sources.items() if q!=p and "IntegrationTests" not in str(q) and any(re.search(r"\b"+re.escape(n)+r"\b",src) for n in names)]
        if callers:calls+="; callers: "+", ".join(dict.fromkeys(callers))[:240]
        tables=list(dict.fromkeys(re.findall(r"\b(?:db|_db)\.(\w+)",s)))
        tables=[x for x in tables if x not in {"Database","ChangeTracker","Entry","SaveChangesAsync"}]
        if not tables and p.suffix==".cs":
            tables=[table for entity,table in entity_tables.items() if re.search(r"\b"+entity+r"\b",s)]
        resources=", ".join(tables) if tables else ("Local DOM/static assets; no SQL" if p.suffix!=".cs" else "Via named contracts; no direct SQL outside Data/startup")
        failures=" outcome/validation and concurrency handling" if "Service" in str(p) else " transport/display errors" if "WebApp" in str(p) else " persistence/contract failures"
        out+=["| "+link(p)+" | "+role(p,s).replace("|","/")+" | "+symbols_text.replace("|","/")+" | "+(calls or "Framework, dependency injection, or containing feature.")+" | "+resources+";"+failures+". Checks: "+feature(p)+" |"]
    out+=[""]
out+=["## Generated and vendor catalogs","","- [Data/Migrations](../CampusGear/CampusGear.Data/Migrations): EF-generated schema history and model snapshot. This refactor changes none of these files. Read ApplicationDbContext for the maintained schema; do not hand-edit generated migration lines.","- [70 Figma Razor frames](../CampusGear/CampusGear.WebApp/Pages/Figma/Frames) and [route/node catalog](../CampusGear/CampusGear.WebApp/wwwroot/figma/catalog.json): generated visual-state references served under /demo. Use [verify.cjs](../figma-reference/verify.cjs) to check their node IDs/assets; edit live Views for live application behavior.","- [figma-generated.css](../CampusGear/CampusGear.WebApp/wwwroot/css/figma-generated.css), [Figma source snapshots](../figma-reference/generated), [assets/fonts](../CampusGear/CampusGear.WebApp/wwwroot/figma): generated/source reference material, not business logic.","- [wwwroot/lib](../CampusGear/CampusGear.WebApp/wwwroot/lib): vendor libraries; custom behavior belongs in wwwroot/js and custom styles above. bin/obj and designer outputs are build artifacts, not maintained source.","","## Supporting operational files","","- [Program.cs](../CampusGear/CampusGear.WebApp/Program.cs) and appsettings/launchSettings configure startup; never commit local secrets.","- [smoke-live.ps1](../scripts/smoke-live.ps1) runs isolated tests and reference validation, without attaching to the live database.","- [setup-gmail.ps1](../scripts/setup-gmail.ps1) configures local sender secrets; it does not create or approve application accounts.","- [PERMISSIONS.md](PERMISSIONS.md) is the human role matrix; RolePermissionTests is its executable check.",""]
(root/"docs"/"SOURCE_CATALOG.md").write_text("\n".join(out),encoding="utf-8")
print(str(len(files))+" maintained files cataloged.")

# Route migration table from current controller declarations. This does not execute the app.
service_names={"service":"UserAdministrationService","categories":"CategoryService","inventory":"InventoryService",
"maintenance":"MaintenanceService","borrower":"BorrowerService","reservations":"ReservationService",
"dashboards":"DashboardService","desk":"CustodianService","directory":"DirectoryService"}
repository_paths={
"AccountService":"AccountRepository.StudentExistsAsync/AddProfile/ReloadUserAsync; Identity EF store; EmailChallengeRepository; AuditRepository",
"UserAdministrationService":"AccountRepository.ListAsync/FindAsync/FindWithProfileAsync/HasHistoryAsync/PrepareDeletionAsync; Identity EF store; AuditRepository.ListAsync/Add",
"CategoryService":"CategoryRepository.ListAsync/FindAsync/HasItemsAsync/IsActiveAsync/Add/Remove; AuditRepository",
"InventoryService":"EquipmentRepository.InventoryAsync/FindAsync/HasHistoryAsync/HasOutstandingLoanAsync; CategoryRepository; MaintenanceRepository; AuditRepository",
"MaintenanceService":"MaintenanceRepository.ListAsync/GetItemIdAsync/FindWithItemAsync/HasOpenAsync/Add; EquipmentRepository; AuditRepository",
"BorrowerService":"BorrowerReadRepository.FindProfileAsync/CatalogAsync/HomeAsync/BlockedAsync/FindRequestsAsync/SubmittedAsync/HistoryAsync/CalendarItemsAsync/CalendarBookingsAsync; ReservationService for writes",
"ReservationService":"ReservationRepository.FindByRequestIdAsync/FindWorkflowAsync/HasConflictAsync/Add; BorrowerProfileRepository; Account/Loan/Equipment/Maintenance/Audit repositories",
"CustodianService":"CustodianReadRepository.ListAsync/DetailsAsync; BorrowerReadRepository.CalendarItemsAsync/CalendarBookingsAsync; EquipmentAvailabilityService",
"DirectoryService":"DirectoryReadRepository.CategoriesAsync/BorrowersAsync",
"DashboardService":"DashboardReadRepository (independent count/trend queries); BorrowerReadRepository.EquipmentStatesAsync; Identity EF reads"}
route_doc=["# Controller to service to repository migration map","","Generated from current controller declarations by [update-code-map.py](../scripts/update-code-map.py). HTTP tests verify endpoint behavior/permissions; this table is for tracing. Query parameters and binding annotations remain in the linked action. For tables/results/worked examples see [FEATURE_TRACES](FEATURE_TRACES.md) and [the beginner guide](CODEBASE_CHEATSHEET.md).","","| URL / HTTP | Controller action and inputs | Application method | Persistence path |","|---|---|---|---|"]
route_count=0
for p in files:
    if "Controllers" not in p.parts or not p.stem.endswith("Controller"):continue
    s=sources[p]
    declarations=list(re.finditer(r"(?m)^\s*public\s+(?:async\s+)?(?:Task<IActionResult>|IActionResult)\s+(\w+)\s*\(",s))
    previous=0
    for i,m in enumerate(declarations):
        attributes=s[previous:m.start()]
        routes=re.findall(r'Http(Get|Post)\("([^"]+)"',attributes)
        if p.stem=="StatusController":routes=[("ANY","status/{code:int}")]
        prefix="auth/" if p.stem=="AccountController" else ""
        end=declarations[i+1].start() if i+1<len(declarations) else len(s)
        body=s[m.start():end]
        direct=[]
        for owner,method in re.findall(r"\b(service|categories|inventory|maintenance|borrower|reservations|dashboards|desk|directory)\.(\w+)\(",body):
            target="AccountService" if p.stem=="AccountController" and owner=="service" else service_names[owner]
            direct.append(target+"."+method)
        key=(p.stem,m[1])
        if not direct:
            special={
                ("AdminUsersController","Borrowers"):["UserAdministrationService.GetPageAsync"],
                ("AdminUsersController","Approve"):["UserAdministrationService.DecideAsync"],
                ("AdminUsersController","Reject"):["UserAdministrationService.DecideAsync"],
                ("CustodianController","AdminHistory"):["CustodianService.GetListAsync"],
                ("CustodianController","AdminCalendar"):["CustodianService.GetCalendarAsync"]}
            direct=special.get(key,[])
        direct=list(dict.fromkeys(direct))
        persistence=list(dict.fromkeys(repository_paths[x.split(".")[0]] for x in direct if x.split(".")[0] in repository_paths))
        opening=body.find("("); depth=1; closing=opening+1
        while closing<len(body) and depth:
            if body[closing]=="(":depth+=1
            if body[closing]==")":depth-=1
            closing+=1
        signature=body[:closing].strip()
        signature=re.sub(r"\s+"," ",signature)
        for verb,path in routes:
            route_count+=1
            if verb=="Get" and p.stem=="AdminUsersController" and m[1]=="Edit":
                direct=["UserAdministrationService.GetEditAsync"]
                persistence=[repository_paths["UserAdministrationService"]]
            route_doc.append("| "+code(verb.upper()+" /"+prefix+path)+" | ["+p.stem+"."+m[1]+"](../"+str(p.relative_to(root)).replace("\\","/")+") — "+signature.replace("|","/")+" | "+(", ".join(code(x) for x in direct) or "Form/status/mailbox HTTP presentation; no business database workflow.")+" | "+("; ".join(persistence) or "No direct database access.")+" |")
        previous=m.start()
route_doc+=["","Repository method clusters list the feature's entry points; open the named application method to see the exact subset and save/lock order. All writes share UnitOfWork/WorkflowLockManager/AuditRepository where the original workflow used them. StatusController accepts any method at its status route. Razor Pages /Error and /demo routes remain outside these MVC controllers.",""]
(root/"docs"/"ROUTE_MIGRATION_MAP.md").write_text("\n".join(route_doc),encoding="utf-8")
print(str(route_count)+" route/action entries cataloged.")
