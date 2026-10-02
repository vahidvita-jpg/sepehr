-- Granular permission keys + default role mapping. SuperAdmin gets everything.
INSERT INTO Permissions(key,description) VALUES
 ('dashboard.view','View dashboard'),
 ('teachers.view','View teachers'),('teachers.manage','Create/edit/delete teachers'),
 ('students.view','View students'),('students.manage','Create/edit/delete students'),('students.import','Import students from Excel'),
 ('timetable.view','View timetables'),('timetable.manage','Edit timetables'),
 ('bells.view','View bells'),('bells.manage','Manage bells and audio'),
 ('attendance.self','Register own attendance'),('attendance.view','View teacher attendance'),('attendance.manage','Edit teacher attendance'),
 ('studentattendance.record','Record student absence/late'),('studentattendance.view','View student attendance'),
 ('grades.enter','Enter grades for own classes'),('grades.view','View all grades'),('grades.edit','Edit any grade'),
 ('discipline.report','Report discipline for own students'),('discipline.view','View all discipline records'),('discipline.manage','Manage discipline records'),
 ('messages.use','Send/receive messages'),('files.send','Send files'),
 ('reports.view','View/export reports'),
 ('backup.view','View backups'),('backup.manage','Create backups'),('backup.restore','Restore backups'),
 ('settings.manage','Change school settings'),('users.manage','Manage users and roles'),('audit.view','View audit log');

-- SuperAdmin: all
INSERT INTO RolePermissions SELECT 1, id FROM Permissions;
-- Principal: everything except role/user management and restore
INSERT INTO RolePermissions SELECT 2, id FROM Permissions WHERE key NOT IN ('users.manage','backup.restore');
-- Teacher
INSERT INTO RolePermissions SELECT 3, id FROM Permissions WHERE key IN
 ('dashboard.view','timetable.view','attendance.self','studentattendance.record','grades.enter','discipline.report','messages.use','files.send');
-- Staff: minimal default, configurable later
INSERT INTO RolePermissions SELECT 4, id FROM Permissions WHERE key IN ('dashboard.view','students.view','messages.use');

INSERT INTO SchemaMigrations(version,name) VALUES (2,'permissions');
