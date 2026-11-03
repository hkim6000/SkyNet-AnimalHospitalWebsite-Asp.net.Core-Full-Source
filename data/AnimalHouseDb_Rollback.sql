USE AnimalHospital;
GO

SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
        DELETE FROM NUMBERSEQ;
        DELETE FROM MESSAGE;
        DELETE FROM ALBUMITEM;
        DELETE FROM ALBUM;
        DELETE FROM MEDIA;
        DELETE FROM PAGEVER;
        DELETE FROM PAGEDOC;
        DELETE FROM REMINDER;
        DELETE FROM MAILLOG;
        DELETE FROM PAYMENT;
        DELETE FROM INVOICELINE;
        DELETE FROM INVOICE;
        DELETE FROM VISITFILE;
        DELETE FROM RESULT;
        DELETE FROM ORDERLINE;
        DELETE FROM DIAGNOSIS;
        DELETE FROM VISITNOTE;
        DELETE FROM VISITROOM;
        DELETE FROM VISIT;
        DELETE FROM APPTLOG;
        DELETE FROM APPOINTMENT;
        DELETE FROM PETVACCINE;
        DELETE FROM PETWEIGHT;
        DELETE FROM PET;
        DELETE FROM CLIENT;
        DELETE FROM HOLIDAY;
        DELETE FROM BOOKSTAFF;
        DELETE FROM BOOKTYPE;
        DELETE FROM TESTFIELD;
        DELETE FROM ITEM;
        DELETE FROM TEAM;
        DELETE FROM ROOM;
        DELETE FROM ROOMTYPE;
        DELETE FROM BREED;
        DELETE FROM SPECIES;
        DELETE FROM ACTIVITY;
        DELETE FROM STAFFSESSION;
        DELETE FROM TIMEOFF;
        DELETE FROM STAFFHOURS;
        DELETE FROM STAFFSPECIES;
        DELETE FROM STAFFPERM;
        DELETE FROM STAFF;
        DELETE FROM ROLEPERM;
        DELETE FROM MENU;
        DELETE FROM ROLE;
        DELETE FROM SETTING;
        INSERT INTO ROLE (ROLE_ID, ROLE_NAME, ROLE_ORDER)
        SELECT V.ROLE_ID, V.ROLE_NAME, V.ROLE_ORDER FROM (VALUES
            (N'manager', N'Manager', 1),
            (N'office', N'Office staff', 2),
            (N'vet', N'Vet', 3),
            (N'tech', N'Technician', 4),
            (N'groomer', N'Groomer', 5),
            (N'editor', N'Website editor', 6)
        ) V (ROLE_ID, ROLE_NAME, ROLE_ORDER)
        WHERE NOT EXISTS (SELECT 1 FROM ROLE X WHERE X.ROLE_ID = V.ROLE_ID);
        INSERT INTO MENU (MENU_KEY, SECTION_KEY, SECTION_NAME, SECTION_ORDER, MENU_NAME, PAGE_NAME, MENU_ORDER, RIGHTS, EXTRA_NAME, HINT)
        SELECT V.MENU_KEY, V.SECTION_KEY, V.SECTION_NAME, V.SECTION_ORDER, V.MENU_NAME, V.PAGE_NAME, V.MENU_ORDER, V.RIGHTS, V.EXTRA_NAME, V.HINT FROM (VALUES
            (N'today', N'front', N'Front office', 1, N'Today board', N'AdminToday', 1, N'VE', N'', N'Arrivals, waiting room, check in, start and checkout'),
            (N'roomboard', N'front', N'Front office', 1, N'Room board', N'AdminRoomBoard', 2, N'VE', N'', N'Who is in every room right now'),
            (N'appts', N'front', N'Front office', 1, N'Appointments', N'AdminAppts', 3, N'VAEX', N'Cancel', N'Book, change and cancel appointments'),
            (N'schedule', N'front', N'Front office', 1, N'Schedule', N'AdminSchedule', 4, N'VE', N'', N'Day and week schedule by vet or room'),
            (N'checkout', N'front', N'Front office', 1, N'Checkout', N'AdminCheckout', 5, N'VE', N'', N'Invoice, payment and email at the end of a visit'),
            (N'visits', N'clinical', N'Clinical', 2, N'Visits', N'AdminVisit', 1, N'VEX', N'Sign records', N'Notes, diagnosis, orders, results and files'),
            (N'charts', N'clinical', N'Clinical', 2, N'Patient charts', N'AdminChart', 2, N'V', N'', N'Whole history of each pet with charts'),
            (N'results', N'clinical', N'Clinical', 2, N'Pending results', N'AdminResults', 3, N'VE', N'', N'Tests waiting for results'),
            (N'reminders', N'clinical', N'Clinical', 2, N'Reminders', N'AdminReminders', 4, N'VE', N'', N'Vaccines, rechecks and wellness exams due'),
            (N'boarding', N'boarding', N'Boarding', 3, N'Boarding board', N'AdminBoarding', 1, N'VAEX', N'Cancel', N'Stays, runs, check-in and check-out'),
            (N'clients', N'people', N'Clients', 4, N'Clients', N'AdminClients', 1, N'VAED', N'', N'Pet owners and their contact details'),
            (N'pets', N'people', N'Clients', 4, N'Pets', N'AdminPets', 2, N'VAED', N'', N'Pet records, weights and vaccines'),
            (N'invoices', N'money', N'Invoices', 5, N'Invoices', N'AdminInvoices', 1, N'VEX', N'Void', N'All invoices with their status'),
            (N'payments', N'money', N'Invoices', 5, N'Payments', N'AdminPayments', 2, N'VAX', N'Void payment', N'Payments taken by method and day'),
            (N'balances', N'money', N'Invoices', 5, N'Balances', N'AdminBalances', 3, N'VE', N'', N'Billed invoices with a balance and statements'),
            (N'items', N'catalog', N'Catalog', 6, N'Items and prices', N'AdminItems', 1, N'VAED', N'', N'Practice, medicine, tests, vaccines, grooming and products'),
            (N'tests', N'catalog', N'Catalog', 6, N'Test fields', N'AdminTests', 2, N'VE', N'', N'Result fields, units and normal ranges'),
            (N'booktypes', N'catalog', N'Catalog', 6, N'Booking types', N'AdminBookTypes', 3, N'VAED', N'', N'What clients can book online and who does it'),
            (N'reports', N'reports', N'Reports', 7, N'Reports', N'AdminReports', 1, N'V', N'', N'Sales, payments, visits and staff'),
            (N'pages', N'website', N'Website', 8, N'Pages', N'AdminPages', 1, N'VE', N'', N'Website text and pictures'),
            (N'media', N'website', N'Website', 8, N'Media library', N'AdminMedia', 2, N'VAED', N'', N'Photos, videos and PDFs'),
            (N'albums', N'website', N'Website', 8, N'Albums', N'AdminAlbums', 3, N'VAED', N'', N'Photo albums for the website'),
            (N'messages', N'website', N'Website', 8, N'Messages', N'AdminMessages', 4, N'VED', N'', N'Contact form messages'),
            (N'staff', N'admin', N'Administration', 9, N'Staff and permissions', N'AdminStaff', 1, N'VAED', N'', N'Sign-ins, roles and menu rights'),
            (N'roomsetup', N'admin', N'Administration', 9, N'Rooms and room types', N'AdminRoomSetup', 2, N'VAED', N'', N'Exam, surgery, recovery, grooming and boarding rooms'),
            (N'hours', N'admin', N'Administration', 9, N'Hours and time off', N'AdminHours', 3, N'VEX', N'Time off', N'Working hours of vets and groomers'),
            (N'settings', N'admin', N'Administration', 9, N'Settings', N'AdminSettings', 4, N'VE', N'', N'Hospital details, hours, booking, tax and email'),
            (N'activity', N'admin', N'Administration', 9, N'Activity log', N'AdminActivity', 5, N'V', N'', N'What everyone did')
        ) V (MENU_KEY, SECTION_KEY, SECTION_NAME, SECTION_ORDER, MENU_NAME, PAGE_NAME, MENU_ORDER, RIGHTS, EXTRA_NAME, HINT)
        WHERE NOT EXISTS (SELECT 1 FROM MENU X WHERE X.MENU_KEY = V.MENU_KEY);
        INSERT INTO ROLEPERM (ROLE_ID, MENU_KEY, RIGHTS)
        SELECT N'manager', MENU_KEY, RIGHTS FROM MENU M
        WHERE NOT EXISTS (SELECT 1 FROM ROLEPERM X WHERE X.ROLE_ID = N'manager' AND X.MENU_KEY = M.MENU_KEY);
        INSERT INTO ROLEPERM (ROLE_ID, MENU_KEY, RIGHTS)
        SELECT V.ROLE_ID, V.MENU_KEY, V.RIGHTS FROM (VALUES
            (N'office', N'today', N'VE'), (N'office', N'roomboard', N'VE'), (N'office', N'appts', N'VAEX'), (N'office', N'schedule', N'VE'), (N'office', N'checkout', N'VE'),
            (N'office', N'charts', N'V'), (N'office', N'reminders', N'VE'), (N'office', N'boarding', N'VAEX'), (N'office', N'clients', N'VAE'), (N'office', N'pets', N'VAE'),
            (N'office', N'invoices', N'VE'), (N'office', N'payments', N'VA'), (N'office', N'balances', N'VE'), (N'office', N'items', N'V'), (N'office', N'messages', N'VE'),
            (N'vet', N'today', N'V'), (N'vet', N'roomboard', N'V'), (N'vet', N'appts', N'VE'), (N'vet', N'schedule', N'V'), (N'vet', N'visits', N'VEX'),
            (N'vet', N'charts', N'V'), (N'vet', N'results', N'VE'), (N'vet', N'reminders', N'V'), (N'vet', N'boarding', N'V'), (N'vet', N'clients', N'V'), (N'vet', N'pets', N'VE'), (N'vet', N'items', N'V'),
            (N'tech', N'today', N'V'), (N'tech', N'roomboard', N'VE'), (N'tech', N'schedule', N'V'), (N'tech', N'visits', N'VE'), (N'tech', N'charts', N'V'),
            (N'tech', N'results', N'VE'), (N'tech', N'reminders', N'VE'), (N'tech', N'boarding', N'VE'), (N'tech', N'clients', N'V'), (N'tech', N'pets', N'VE'), (N'tech', N'items', N'V'),
            (N'groomer', N'today', N'V'), (N'groomer', N'appts', N'V'), (N'groomer', N'schedule', N'V'), (N'groomer', N'visits', N'VE'), (N'groomer', N'clients', N'V'), (N'groomer', N'pets', N'V'),
            (N'editor', N'pages', N'VE'), (N'editor', N'media', N'VAED'), (N'editor', N'albums', N'VAED'), (N'editor', N'messages', N'VED'), (N'editor', N'booktypes', N'V')
        ) V (ROLE_ID, MENU_KEY, RIGHTS)
        WHERE NOT EXISTS (SELECT 1 FROM ROLEPERM X WHERE X.ROLE_ID = V.ROLE_ID AND X.MENU_KEY = V.MENU_KEY);
        INSERT INTO SPECIES (SPECIES_KEY, SPECIES_NAME, PLURAL_NAME, HAS_SIZE, SPECIES_ORDER)
        SELECT V.SPECIES_KEY, V.SPECIES_NAME, V.PLURAL_NAME, V.HAS_SIZE, V.SPECIES_ORDER FROM (VALUES
            (N'dog', N'Dog', N'Dogs', 1, 1),
            (N'cat', N'Cat', N'Cats', 0, 2),
            (N'rabbit', N'Rabbit', N'Rabbits', 0, 3),
            (N'small', N'Small mammal', N'Small mammals', 0, 4),
            (N'bird', N'Bird', N'Birds', 0, 5),
            (N'reptile', N'Reptile', N'Reptiles', 0, 6)
        ) V (SPECIES_KEY, SPECIES_NAME, PLURAL_NAME, HAS_SIZE, SPECIES_ORDER)
        WHERE NOT EXISTS (SELECT 1 FROM SPECIES X WHERE X.SPECIES_KEY = V.SPECIES_KEY);
        INSERT INTO BREED (BREED_ID, SPECIES_KEY, BREED_NAME, SIZE_HINT)
        SELECT V.BREED_ID, V.SPECIES_KEY, V.BREED_NAME, V.SIZE_HINT FROM (VALUES
            (N'br-001', N'dog', N'Australian Shepherd', N'medium'),
            (N'br-002', N'dog', N'Beagle', N'small'),
            (N'br-003', N'dog', N'Bernese mountain dog', N'large'),
            (N'br-004', N'dog', N'Border Collie', N'medium'),
            (N'br-005', N'dog', N'Border collie', N'medium'),
            (N'br-006', N'dog', N'Boston Terrier', N'small'),
            (N'br-007', N'dog', N'Boxer', N'large'),
            (N'br-008', N'dog', N'Bulldog', N'medium'),
            (N'br-009', N'dog', N'Cavalier King Charles Spaniel', N'small'),
            (N'br-010', N'dog', N'Chihuahua', N'small'),
            (N'br-011', N'dog', N'Corgi', N'medium'),
            (N'br-012', N'dog', N'Dachshund', N'small'),
            (N'br-013', N'dog', N'French Bulldog', N'small'),
            (N'br-014', N'dog', N'French bulldog', N'medium'),
            (N'br-015', N'dog', N'German Shepherd', N'large'),
            (N'br-016', N'dog', N'German shepherd', N'large'),
            (N'br-017', N'dog', N'Golden Retriever', N'large'),
            (N'br-018', N'dog', N'Golden retriever', N'large'),
            (N'br-019', N'dog', N'Goldendoodle', N'medium'),
            (N'br-020', N'dog', N'Great Dane', N'large'),
            (N'br-021', N'dog', N'Labradoodle', N'large'),
            (N'br-022', N'dog', N'Labrador Retriever', N'large'),
            (N'br-023', N'dog', N'Labrador mix', N'large'),
            (N'br-024', N'dog', N'Mixed breed', N'medium'),
            (N'br-025', N'dog', N'Pembroke Welsh Corgi', N'medium'),
            (N'br-026', N'dog', N'Pit Bull Terrier', N'medium'),
            (N'br-027', N'dog', N'Poodle (Miniature)', N'small'),
            (N'br-028', N'dog', N'Poodle (Standard)', N'large'),
            (N'br-029', N'dog', N'Pug', N'small'),
            (N'br-030', N'dog', N'Rottweiler', N'large'),
            (N'br-031', N'dog', N'Shiba inu', N'medium'),
            (N'br-032', N'dog', N'Shih Tzu', N'small'),
            (N'br-033', N'dog', N'Siberian Husky', N'large'),
            (N'br-034', N'dog', N'Yorkshire Terrier', N'small'),
            (N'br-035', N'cat', N'Abyssinian', N''),
            (N'br-036', N'cat', N'Bengal', N''),
            (N'br-037', N'cat', N'British Shorthair', N''),
            (N'br-038', N'cat', N'Domestic Longhair', N''),
            (N'br-039', N'cat', N'Domestic Shorthair', N''),
            (N'br-040', N'cat', N'Domestic longhair', N''),
            (N'br-041', N'cat', N'Domestic shorthair', N''),
            (N'br-042', N'cat', N'Maine Coon', N''),
            (N'br-043', N'cat', N'Maine coon', N''),
            (N'br-044', N'cat', N'Persian', N''),
            (N'br-045', N'cat', N'Ragdoll', N''),
            (N'br-046', N'cat', N'Siamese', N''),
            (N'br-047', N'cat', N'Sphynx', N''),
            (N'br-048', N'cat', N'Tabby', N''),
            (N'br-049', N'rabbit', N'Flemish Giant', N''),
            (N'br-050', N'rabbit', N'Holland Lop', N''),
            (N'br-051', N'rabbit', N'Holland lop', N''),
            (N'br-052', N'rabbit', N'Lionhead', N''),
            (N'br-053', N'rabbit', N'Mini Rex', N''),
            (N'br-054', N'rabbit', N'Netherland Dwarf', N''),
            (N'br-055', N'rabbit', N'Netherland dwarf', N''),
            (N'br-056', N'small', N'Chinchilla', N''),
            (N'br-057', N'small', N'Ferret', N''),
            (N'br-058', N'small', N'Guinea pig', N''),
            (N'br-059', N'small', N'Hamster', N''),
            (N'br-060', N'small', N'Rat', N''),
            (N'br-061', N'bird', N'African Grey', N''),
            (N'br-062', N'bird', N'Budgerigar', N''),
            (N'br-063', N'bird', N'Budgie', N''),
            (N'br-064', N'bird', N'Cockatiel', N''),
            (N'br-065', N'bird', N'Conure', N''),
            (N'br-066', N'bird', N'Lovebird', N''),
            (N'br-067', N'reptile', N'Ball Python', N''),
            (N'br-068', N'reptile', N'Bearded Dragon', N''),
            (N'br-069', N'reptile', N'Bearded dragon', N''),
            (N'br-070', N'reptile', N'Corn Snake', N''),
            (N'br-071', N'reptile', N'Leopard Gecko', N''),
            (N'br-072', N'reptile', N'Red-eared Slider', N'')
        ) V (BREED_ID, SPECIES_KEY, BREED_NAME, SIZE_HINT)
        WHERE NOT EXISTS (SELECT 1 FROM BREED X WHERE X.SPECIES_KEY = V.SPECIES_KEY AND X.BREED_NAME = V.BREED_NAME);
        INSERT INTO SETTING (SKEY, SVALUE)
        SELECT V.SKEY, V.SVALUE FROM (VALUES
            (N'name', N'Animal Hospital'),
            (N'tagline', N''),
            (N'town', N''),
            (N'address', N''),
            (N'phone', N''),
            (N'email', N''),
            (N'urgent', N''),
            (N'er.name', N''),
            (N'er.phone', N''),
            (N'er.address', N''),
            (N'accent', N'#3F7D6B'),
            (N'theme', N'light'),
            (N'timezone', N'America/New_York'),
            (N'clock24', N'0'),
            (N'hours', N'0|0|09:00|13:00;1|1|07:30|19:00;2|1|07:30|19:00;3|1|07:30|19:00;4|1|07:30|19:00;5|1|07:30|19:00;6|1|08:00|16:00'),
            (N'slot.minutes', N'15'),
            (N'book.notice.hours', N'2'),
            (N'book.ahead.days', N'60'),
            (N'cancel.hours', N'24'),
            (N'show.prices', N'1'),
            (N'stay.checkin.from', N'07:30'),
            (N'stay.checkin.to', N'18:00'),
            (N'stay.checkout.by', N'11:00'),
            (N'stay.max.nights', N'30'),
            (N'stay.cancel.hours', N'48'),
            (N'stay.vax.dog', N'Rabies,DHPP,Bordetella'),
            (N'stay.vax.cat', N'Rabies,FVRCP'),
            (N'remind.days', N'30'),
            (N'wellness.months', N'12'),
            (N'tax.rate', N'0'),
            (N'invoice.due.days', N'30'),
            (N'invoice.note', N'Thank you for trusting us with your pet.'),
            (N'mail.after.checkout', N'1'),
            (N'insurance', N''),
            (N'video.mb', N'25')
        ) V (SKEY, SVALUE)
        WHERE NOT EXISTS (SELECT 1 FROM SETTING X WHERE X.SKEY = V.SKEY);
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO

IF OBJECT_ID(N'XSP_DEMO_LOAD', N'P') IS NOT NULL
    DROP PROCEDURE XSP_DEMO_LOAD;
GO
