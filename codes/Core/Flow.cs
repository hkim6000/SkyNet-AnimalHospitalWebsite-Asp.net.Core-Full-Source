using System.Data;
using AnimalHospital.codes;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;

namespace AnimalHospital.Core
{
    public static class Flow
    {
        public const string Busy = "ROOMBUSY";
        public const string Moved = "STATECHANGED";

        public static async Task<string> NextNo(string key, string prefix)
        {
            var (dt, emsg) = await Helper.GetDataTable("DECLARE @n NVARCHAR(20); EXEC XSP_NEXT_NO @k, @p, @n OUTPUT; SELECT @n AS N;", new List<SqlParameter> { App.P("k", key, 20), App.P("p", prefix, 10) });
            return emsg == string.Empty && dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0][0]) ?? string.Empty : string.Empty;
        }

        public static string Friendly(string result)
        {
            if (result.Contains(Busy, StringComparison.OrdinalIgnoreCase))
            {
                return "That room was just taken. Pick another room.";
            }
            if (result.Contains(Moved, StringComparison.OrdinalIgnoreCase))
            {
                return "Someone else just changed this visit. The board is refreshed.";
            }
            return "This could not be saved. Try again.";
        }

        public static async Task<List<RoomPick>> Rooms()
        {
            var (list, emsg) = await Helper.GetListT<RoomPick>("SELECT R.ROOM_ID AS RoomId, R.ROOM_NAME AS RoomName, R.ROOMTYPE_ID AS RoomtypeId, T.TYPE_NAME AS TypeName, T.PURPOSE AS Purpose, T.SPECIES AS Species, T.TYPE_ORDER AS TypeOrder, R.ROOM_ORDER AS RoomOrder, ISNULL(X.VISIT_ID, N'') AS BusyVisit, ISNULL(P.PET_NAME, N'') AS BusyPet FROM ROOM R JOIN ROOMTYPE T ON T.ROOMTYPE_ID = R.ROOMTYPE_ID LEFT JOIN VISITROOM X ON X.ROOM_ID = R.ROOM_ID AND X.OUT_AT IS NULL LEFT JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE R.ACTIVE = 1 AND T.ACTIVE = 1 AND T.PURPOSE <> N'boarding' ORDER BY T.TYPE_ORDER, R.ROOM_ORDER, R.ROOM_NAME");
            return emsg == string.Empty && list != null ? list : new List<RoomPick>();
        }

        public static async Task<List<StaffPick>> Providers()
        {
            var (list, emsg) = await Helper.GetListT<StaffPick>("SELECT STAFF_ID AS StaffId, STAFF_NAME AS StaffName, PROVIDER_TYPE AS ProviderType, HOME_ROOM_ID AS HomeRoomId, COLOR AS Color, PHOTO AS Photo, OWN_ONLY AS OwnOnly FROM STAFF WHERE ACTIVE = 1 AND PROVIDER_TYPE <> N'' ORDER BY CASE PROVIDER_TYPE WHEN N'vet' THEN 1 WHEN N'tech' THEN 2 ELSE 3 END, STAFF_NAME");
            return emsg == string.Empty && list != null ? list : new List<StaffPick>();
        }

        private static async Task<FlowAppt?> Appt(string apptId)
        {
            var (row, emsg) = await Helper.GetOneT<FlowAppt>("SELECT A.APPT_ID AS ApptId, A.CLIENT_ID AS ClientId, A.PET_ID AS PetId, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, A.VISIT_TYPE AS VisitType, A.STAFF_ID AS StaffId, A.STATUS AS Status, A.VISIT_ID AS VisitId, A.START_AT AS StartAt, A.PICKUP_AT AS PickupAt FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID WHERE A.APPT_ID = @aid", new List<SqlParameter> { App.P("aid", apptId, 50) });
            return emsg == string.Empty ? row : null;
        }

        public static async Task<FlowVisit?> Visit(string visitId)
        {
            var (row, emsg) = await Helper.GetOneT<FlowVisit>("SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.APPT_ID AS ApptId, ISNULL(P.PET_NAME, N'') AS PetName, V.VISIT_TYPE AS VisitType, V.STATUS AS Status, V.STAFF_ID AS StaffId FROM VISIT V LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE V.VISIT_ID = @vid", new List<SqlParameter> { App.P("vid", visitId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static string LogSql(string idParam)
        {
            return "INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, " + idParam + ", SYSUTCDATETIME(), @me, @ltext)";
        }

        public static async Task<string> Log(Ctx c, string apptId, string text)
        {
            return await Helper.PutData(new List<string> { LogSql("@aid") }, new List<SqlParameter> { App.P("lid", App.NewId(), 50), App.P("aid", apptId, 50), App.P("me", c.U.StaffId, 50), App.P("ltext", Ui.Clip(text, 300), 300) });
        }

        public static async Task<(string VisitId, string Error)> CheckIn(Ctx c, string apptId, Dictionary<string, string> s)
        {
            FlowAppt? a = await Appt(apptId);
            if (a == null)
            {
                return (string.Empty, "This appointment was not found.");
            }
            if (a.VisitType == "boarding")
            {
                return (string.Empty, "Boarding stays are checked in on the Boarding board.");
            }
            if (a.Status != "booked" && a.Status != "confirmed")
            {
                return (string.Empty, a.Status == "checkedin" ? "This pet is already checked in." : "Only booked or confirmed appointments can be checked in.");
            }
            DateTime now = App.Now(s);
            if (a.StartAt.Date != now.Date)
            {
                return (string.Empty, "Only today's appointments can be checked in.");
            }
            string vno = await NextNo("VIS", "V");
            if (vno == string.Empty)
            {
                return (string.Empty, "A visit number could not be made. Check the database.");
            }
            string vid = App.NewId();
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("vid", vid, 50), App.P("vno", vno, 20), App.P("aid", a.ApptId, 50), App.P("cid", a.ClientId, 50), App.P("pid", a.PetId, 50),
                App.P("vt", a.VisitType, 20), App.P("sid", a.StaffId, 50), App.P("why", Ui.Clip(a.BookName, 300), 300), App.PDate("now", now), App.PDate("plan", a.PickupAt),
                App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", a.Status == "booked" ? "Checked in (was booked)" : "Checked in", 300)
            };
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE APPOINTMENT SET STATUS = N'checkedin', VISIT_ID = @vid, SYSDTE = GETDATE() WHERE APPT_ID = @aid AND STATUS IN (N'booked', N'confirmed'); IF @@ROWCOUNT = 0 THROW 50002, N'" + Moved + "', 1;",
                "INSERT INTO VISIT (VISIT_ID, VISIT_NO, APPT_ID, CLIENT_ID, PET_ID, VISIT_TYPE, STATUS, STAFF_ID, REASON, CHECKIN_AT, PLAN_OUT, CREATED_BY) VALUES (@vid, @vno, @aid, @cid, @pid, @vt, N'waiting', @sid, @why, @now, @plan, @me)",
                LogSql("@aid")
            }, ps);
            if (Helper.IsError(r))
            {
                return (string.Empty, Friendly(r));
            }
            await App.Log(c.U.StaffId, "check in", a.PetName + " · " + a.BookName, true);
            return (vid, string.Empty);
        }

        public static async Task<(string VisitId, string Error)> WalkIn(Ctx c, string petId, string visitType, string staffId, string reason, Dictionary<string, string> s)
        {
            var (pet, perr) = await Helper.GetDataTable("SELECT P.CLIENT_ID, P.PET_NAME FROM PET P JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID WHERE P.PET_ID = @pid AND P.ACTIVE = 1", new List<SqlParameter> { App.P("pid", petId, 50) });
            if (perr != string.Empty || pet.Rows.Count == 0)
            {
                return (string.Empty, "Pick a pet first.");
            }
            if (!new[] { "clinic", "surgery", "grooming" }.Contains(visitType))
            {
                return (string.Empty, "Pick the kind of visit.");
            }
            var (open, oerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM VISIT WHERE PET_ID = @pid AND STATUS IN (N'waiting', N'inroom', N'checkout') AND VISIT_TYPE <> N'boarding'", new List<SqlParameter> { App.P("pid", petId, 50) });
            if (oerr == string.Empty && Convert.ToInt32(open.Rows[0][0]) > 0)
            {
                return (string.Empty, "This pet already has an open visit today.");
            }
            string vno = await NextNo("VIS", "V");
            if (vno == string.Empty)
            {
                return (string.Empty, "A visit number could not be made. Check the database.");
            }
            string vid = App.NewId();
            string why = Ui.OneLine(reason, 300);
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("vid", vid, 50), App.P("vno", vno, 20), App.P("cid", Convert.ToString(pet.Rows[0][0]), 50), App.P("pid", petId, 50),
                App.P("vt", visitType, 20), App.P("sid", staffId, 50), App.P("why", why == string.Empty ? "Walk-in" : why, 300), App.PDate("now", App.Now(s)), App.P("me", c.U.StaffId, 50)
            };
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE PET SET SYSDTE = SYSDTE WHERE PET_ID = @pid",
                "IF EXISTS (SELECT 1 FROM VISIT WHERE PET_ID = @pid AND STATUS IN (N'waiting', N'inroom', N'checkout') AND VISIT_TYPE <> N'boarding') THROW 50004, N'PETOPEN', 1;",
                "INSERT INTO VISIT (VISIT_ID, VISIT_NO, APPT_ID, CLIENT_ID, PET_ID, VISIT_TYPE, STATUS, STAFF_ID, REASON, CHECKIN_AT, CREATED_BY) VALUES (@vid, @vno, N'', @cid, @pid, @vt, N'waiting', @sid, @why, @now, @me)"
            }, ps);
            if (Helper.IsError(r))
            {
                return (string.Empty, r.Contains("PETOPEN", StringComparison.OrdinalIgnoreCase) ? "This pet already has an open visit today." : Friendly(r));
            }
            await App.Log(c.U.StaffId, "walk-in", Convert.ToString(pet.Rows[0][1]) + " · " + (why == string.Empty ? "Walk-in" : why), true);
            return (vid, string.Empty);
        }

        public static async Task<string> UndoCheckIn(Ctx c, string visitId)
        {
            FlowVisit? v = await Visit(visitId);
            if (v == null || v.Status != "waiting")
            {
                return "Only a waiting visit can be undone.";
            }
            string back = "confirmed";
            if (v.ApptId != string.Empty)
            {
                var (lt, lerr) = await Helper.GetDataTable("SELECT TOP 1 LOG_TEXT FROM APPTLOG WHERE APPT_ID = @aid AND LOG_TEXT LIKE N'Checked in%' ORDER BY LOG_UTC DESC", new List<SqlParameter> { App.P("aid", v.ApptId, 50) });
                if (lerr == string.Empty && lt.Rows.Count > 0 && (Convert.ToString(lt.Rows[0][0]) ?? string.Empty).Contains("was booked"))
                {
                    back = "booked";
                }
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", visitId, 50), App.P("aid", v.ApptId, 50), App.P("back", back, 20), App.PDate("now", App.Now(await App.Settings())), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", "Check-in undone", 300) };
            List<string> sql = new List<string>
            {
                "UPDATE VISIT SET STATUS = N'cancelled', CLOSED_AT = @now, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS = N'waiting'; IF @@ROWCOUNT = 0 THROW 50002, N'" + Moved + "', 1;"
            };
            if (v.ApptId != string.Empty)
            {
                sql.Add("UPDATE APPOINTMENT SET STATUS = @back, VISIT_ID = N'', SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_ID = @vid");
                sql.Add(LogSql("@aid"));
            }
            string r = await Helper.PutData(sql, ps);
            if (Helper.IsError(r))
            {
                return Friendly(r);
            }
            await App.Log(c.U.StaffId, "undo check in", v.PetName + " · " + v.VisitNo, true);
            return string.Empty;
        }

        public static async Task<string> SetApptStatus(Ctx c, string apptId, string status, string reason)
        {
            FlowAppt? a = await Appt(apptId);
            if (a == null)
            {
                return "This appointment was not found.";
            }
            if (a.Status != "booked" && a.Status != "confirmed")
            {
                return "Only booked or confirmed appointments can be changed this way.";
            }
            string text = status switch { "noshow" => "Marked as no-show", "cancelled" => "Cancelled" + (reason != string.Empty ? ": " + reason : string.Empty), "confirmed" => "Confirmed with the owner", _ => "Set back to booked" };
            List<SqlParameter> ps = new List<SqlParameter> { App.P("aid", apptId, 50), App.P("st", status, 20), App.P("why", Ui.Clip(reason, 200), 200), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip(text, 300), 300) };
            string r = await Helper.PutData(new List<string>
            {
                "IF NOT EXISTS (SELECT 1 FROM APPOINTMENT WHERE APPT_ID = @aid AND STATUS IN (N'booked', N'confirmed')) THROW 50002, N'" + Moved + "', 1;",
                "UPDATE APPOINTMENT SET STATUS = @st, CANCEL_REASON = CASE WHEN @st = N'cancelled' THEN @why ELSE CANCEL_REASON END, SYSDTE = GETDATE() WHERE APPT_ID = @aid",
                LogSql("@aid")
            }, ps);
            if (Helper.IsError(r))
            {
                return Friendly(r);
            }
            await App.Log(c.U.StaffId, status == "noshow" ? "no-show" : status == "cancelled" ? "cancel appointment" : "confirm appointment", a.PetName + " · " + a.BookName, true);
            return string.Empty;
        }

        public static async Task<string> Start(Ctx c, string visitId, string roomId, string staffId, Dictionary<string, string> s)
        {
            FlowVisit? v = await Visit(visitId);
            if (v == null || v.Status != "waiting")
            {
                return "Only a waiting visit can be started.";
            }
            RoomPick? room = (await Rooms()).FirstOrDefault(r => r.RoomId == roomId);
            if (room == null)
            {
                return "Pick a room.";
            }
            if (room.BusyVisit != string.Empty)
            {
                return room.RoomName + " is in use by " + room.BusyPet + ".";
            }
            StaffPick? st = (await Providers()).FirstOrDefault(x => x.StaffId == staffId);
            if (st == null)
            {
                return "Pick who sees this patient.";
            }
            DateTime now = App.Now(s);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", visitId, 50), App.P("room", roomId, 50), App.P("sid", staffId, 50), App.P("pur", room.Purpose, 20), App.P("vr", App.NewId(), 50), App.PDate("now", now), App.P("aid", v.ApptId, 50), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip("Started in " + room.RoomName + " with " + st.StaffName, 300), 300) };
            List<string> sql = new List<string>
            {
                "UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room",
                "IF EXISTS (SELECT 1 FROM VISITROOM WHERE ROOM_ID = @room AND OUT_AT IS NULL) THROW 50001, N'" + Busy + "', 1;",
                "UPDATE VISIT SET STATUS = N'inroom', STAFF_ID = @sid, START_AT = @now, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS = N'waiting'; IF @@ROWCOUNT = 0 THROW 50002, N'" + Moved + "', 1;",
                "INSERT INTO VISITROOM (VR_ID, VISIT_ID, ROOM_ID, STAFF_ID, PURPOSE, IN_AT) VALUES (@vr, @vid, @room, @sid, @pur, @now)"
            };
            if (v.ApptId != string.Empty)
            {
                sql.Add(LogSql("@aid"));
            }
            string r = await Helper.PutData(sql, ps);
            if (Helper.IsError(r))
            {
                return Friendly(r);
            }
            await App.Log(c.U.StaffId, "start visit", v.PetName + " · " + room.RoomName, true);
            return string.Empty;
        }

        public static async Task<string> Move(Ctx c, string visitId, string roomId, string staffId, Dictionary<string, string> s)
        {
            FlowVisit? v = await Visit(visitId);
            if (v == null || v.Status != "inroom" || v.VisitType == "boarding")
            {
                return "Only a visit in a room can be moved.";
            }
            RoomPick? room = (await Rooms()).FirstOrDefault(r => r.RoomId == roomId);
            if (room == null)
            {
                return "Pick a room.";
            }
            if (room.BusyVisit == visitId)
            {
                return "The patient is already in " + room.RoomName + ".";
            }
            if (room.BusyVisit != string.Empty)
            {
                return room.RoomName + " is in use by " + room.BusyPet + ".";
            }
            string sid = staffId == string.Empty ? v.StaffId : staffId;
            if (!(await Providers()).Any(x => x.StaffId == sid))
            {
                return "Pick who looks after the patient.";
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", visitId, 50), App.P("room", roomId, 50), App.P("sid", sid, 50), App.P("pur", room.Purpose, 20), App.P("vr", App.NewId(), 50), App.PDate("now", App.Now(s)) };
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room",
                "IF EXISTS (SELECT 1 FROM VISITROOM WHERE ROOM_ID = @room AND OUT_AT IS NULL) THROW 50001, N'" + Busy + "', 1;",
                "UPDATE VISIT SET SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS = N'inroom'; IF @@ROWCOUNT = 0 THROW 50002, N'" + Moved + "', 1;",
                "UPDATE VISITROOM SET OUT_AT = @now WHERE VISIT_ID = @vid AND OUT_AT IS NULL",
                "INSERT INTO VISITROOM (VR_ID, VISIT_ID, ROOM_ID, STAFF_ID, PURPOSE, IN_AT) VALUES (@vr, @vid, @room, @sid, @pur, @now)",
                "UPDATE VISIT SET STAFF_ID = CASE WHEN @pur IN (N'recovery') THEN STAFF_ID ELSE @sid END, SYSDTE = GETDATE() WHERE VISIT_ID = @vid"
            }, ps);
            if (Helper.IsError(r))
            {
                return Friendly(r);
            }
            await App.Log(c.U.StaffId, "move room", v.PetName + " · " + room.RoomName, true);
            return string.Empty;
        }

        public static async Task<string> Ready(Ctx c, string visitId, Dictionary<string, string> s)
        {
            FlowVisit? v = await Visit(visitId);
            if (v == null || v.Status != "inroom" || v.VisitType == "boarding")
            {
                return "Only a visit in a room can be sent to checkout.";
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", visitId, 50), App.PDate("now", App.Now(s)) };
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE VISIT SET STATUS = N'checkout', READY_AT = @now, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS = N'inroom'; IF @@ROWCOUNT = 0 THROW 50002, N'" + Moved + "', 1;",
                "UPDATE VISITROOM SET OUT_AT = @now WHERE VISIT_ID = @vid AND OUT_AT IS NULL"
            }, ps);
            if (Helper.IsError(r))
            {
                return Friendly(r);
            }
            await App.Log(c.U.StaffId, "ready for checkout", v.PetName + " · " + v.VisitNo, true);
            return string.Empty;
        }
    }
}
