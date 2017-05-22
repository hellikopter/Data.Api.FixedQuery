﻿namespace Parliament.Data.Api.FixedQuery.Controllers
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Web.Http;
    using VDS.RDF;
    using VDS.RDF.Query;

    [RoutePrefix("parliaments")]
    public class ParliamentController : BaseController
    {
        [Route("", Name = "ParliamentIndex")]
        [HttpGet]
        public Graph Index()
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber .
}
WHERE {
    ?parliament 
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodNumber ?parliamentNumber .
    OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
}
";

            var query = new SparqlParameterizedString(queryString);

            return BaseController.ExecuteList(query);
        }

        [Route("current", Name = "ParliamentCurrent")]
        [HttpGet]
        public Graph Current()
        {
            var queryString = @"
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod .
}
WHERE {
    ?parliament a :ParliamentPeriod ;
                :parliamentPeriodStartDate ?startDate .
    FILTER NOT EXISTS { ?parliament a :PastParliamentPeriod }
    BIND(xsd:dateTime(?startDate) AS ?startDateTime)
    BIND(now() AS ?currentDate)
    FILTER(?startDateTime < ?currentDate)
}
";

            var query = new SparqlParameterizedString(queryString);

            return BaseController.ExecuteSingle(query);
        }

        [Route("previous", Name = "ParliamentPrevious")]
        [HttpGet]
        public Graph Previous()
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?previousParliament
        a :ParliamentPeriod .
}
WHERE {
    {
        ?parliament a :ParliamentPeriod .
        FILTER NOT EXISTS { ?parliament a :PastParliamentPeriod }
        ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .

    }
    UNION {
        ?parliament a :ParliamentPeriod .
        {  SELECT (max(?parliamentPeriodEndDate) AS ?maxEndDate) 
          WHERE {
              ?parliament a :ParliamentPeriod ;
                        :parliamentPeriodEndDate ?parliamentPeriodEndDate .
          }
   		}
        ?parliament :parliamentPeriodEndDate ?maxEndDate .
        BIND(?parliament AS ?previousParliament)
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            return BaseController.ExecuteList(query);
        }

        [Route("next", Name = "ParliamentNext")]
        [HttpGet]
        public Graph Next()
        {
            var queryString = @"
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?nextParliament
        a :ParliamentPeriod .
}
WHERE {
    ?nextParliament a :ParliamentPeriod ;
                    :parliamentPeriodStartDate ?startDate .
    BIND(now() AS ?currentDate)
    BIND(xsd:dateTime(?startDate) AS ?startDateTime)
    FILTER(?startDateTime > ?currentDate)
}";

            var query = new SparqlParameterizedString(queryString);

            return BaseController.ExecuteList(query);
        }

        // Ruby route: get '/parliaments/lookup', to: 'parliaments#lookup'
        [Route(@"lookup/{source:regex(^\w+$)}/{id}", Name = "ParliamentLookup")]
        [HttpGet]
        public Graph Lookup(string source, string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament a :ParliamentPeriod .
}
WHERE {
    BIND(@id AS ?id)
    BIND(@source AS ?source)
    ?parliament
        a :ParliamentPeriod ;
        ?source ?id .
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("source", new Uri(BaseController.schema, source));
            query.SetLiteral("id", id);

            return BaseController.ExecuteList(query);
        }

        [Route(@"{id:regex(^\w{8}$)}", Name = "ParliamentById")]
        [HttpGet]
        public Graph ById(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber ;
        :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	:parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?party
        a :Party ;
        :partyName ?partyName ;
        :count ?memberCount .
}
WHERE {
    SELECT ?parliament ?startDate ?endDate ?parliamentNumber ?party ?partyName ?nextParliament ?previousParliament (COUNT(?member) AS ?memberCount)
    WHERE {
        BIND(@id AS ?parliament)
        ?parliament
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?startDate ;
            :parliamentPeriodNumber ?parliamentNumber .
            OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
        	OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
            OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }

        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyStartDate ?incStartDate ;                
           					:incumbencyHasMember ?member .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?incumbencyEndDate . }
            ?member :partyMemberHasPartyMembership ?partyMembership .
            ?partyMembership :partyMembershipHasParty ?party ;
                             :partyMembershipStartDate ?pmStartDate .
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }
            ?party :partyName ?partyName .
            
            BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
    		BIND(COALESCE(?incumbencyEndDate,now()) AS ?incEndDate)
            FILTER (
                (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
                (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
            )
        }
    }
	GROUP BY ?parliament ?startDate ?endDate ?parliamentNumber ?party ?partyName ?nextParliament ?previousParliament
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("id", new Uri(BaseController.instance, id));

            return BaseController.ExecuteSingle(query);

        }

        [Route(@"{id:regex(^\w{8}$)}/next", Name = "NextParliamentById")]
        [HttpGet]
        public Graph Next(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?nextParliament
        a :ParliamentPeriod .
}
WHERE {
    BIND(@id AS ?parliament)
    
    ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament .
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("id", new Uri(BaseController.instance, id));

            return BaseController.ExecuteSingle(query);

        }

        [Route(@"{id:regex(^\w{8}$)}/previous", Name = "PreviousParliamentById")]
        [HttpGet]
        public Graph Previous(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?previousParliament
        a :ParliamentPeriod .
}
WHERE {
    BIND(@id AS ?parliament)
    
    ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("id", new Uri(BaseController.instance, id));

            return BaseController.ExecuteSingle(query);

        }

        [Route(@"{id:regex(^\w{8}$)}/members", Name = "ParliamentMembers")]
        [HttpGet]
        public Graph Members(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
        BIND(@parliamentid AS ?parliament)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?parliamentStartDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?person ;
                            :seatIncumbencyHasHouseSeat ?houseSeat ;
                			:incumbencyStartDate ?incStartDate .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup ;
                       :houseSeatHasHouse ?house .
            ?house :houseName ?houseName .
            ?constituencyGroup :constituencyGroupName ?constituencyName .

            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
                
            ?person :partyMemberHasPartyMembership ?partyMembership .
            ?partyMembership :partyMembershipHasParty ?party ;
                                :partyMembershipStartDate ?pmStartDate .
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               
            ?party :partyName ?partyName .

            BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
            BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
            FILTER (
                (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
                (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
            )
        }
    }
}
UNION {
    SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)

        ?parliament a :ParliamentPeriod ;                
        			:parliamentPeriodHasSeatIncumbency ?seatIncumbency.
        ?seatIncumbency :incumbencyHasMember ?person .
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
    }
  }
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, id));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/members/{initial:regex(^\p{L}+$):maxlength(1)}", Name = "ParliamentMembersByInitial")]
        [HttpGet]
        public Graph MembersByInitial(string parliamentid, string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
        BIND(@parliamentid AS ?parliament)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?parliamentStartDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?person ;
                            :seatIncumbencyHasHouseSeat ?houseSeat ;
                			:incumbencyStartDate ?incStartDate .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup ;
                       :houseSeatHasHouse ?house .
            ?house :houseName ?houseName .
            ?constituencyGroup :constituencyGroupName ?constituencyName .

            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
                
                ?person :partyMemberHasPartyMembership ?partyMembership .
                ?partyMembership :partyMembershipHasParty ?party ;
                                 :partyMembershipStartDate ?pmStartDate .
                OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               
                ?party :partyName ?partyName .

                BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
                BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
                FILTER (
                    (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
                    (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
                )
        FILTER STRSTARTS(LCASE(?listAs), LCASE(@initial))
        }
    }
}
UNION {
    SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)

        ?parliament a :ParliamentPeriod ;                
        			:parliamentPeriodHasSeatIncumbency ?seatIncumbency.
        ?seatIncumbency :incumbencyHasMember ?person .
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
    }
  }
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetLiteral("initial", initial);

            return BaseController.ExecuteList(query);
        }

        [Route(@"{id:regex(^\w{8}$)}/members/a_z_letters", Name = "ParliamentMembersAToZ")]
        [HttpGet]
        public Graph MembersAToZLetters(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    _:x :value ?firstLetter .
}
WHERE {
    SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)

        ?parliament a :ParliamentPeriod ;                
        			:parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person .
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
    }
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, id));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{id:regex(^\w{8}$)}/members/houses", Name = "ParliamentMembersHouses")]
        [HttpGet]
        public Graph MembersHouses(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
	 ?house
        a :House ;
        :houseName ?houseName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
}
WHERE {
    BIND(@parliamentid AS ?parliament)
    ?parliament 
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?parliamentStartDate ;
        :parliamentPeriodNumber ?parliamentNumber .
    OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }

    OPTIONAL {
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person ;
                        :seatIncumbencyHasHouseSeat ?houseSeat .
        ?houseSeat :houseSeatHasHouse ?house .
        ?house :houseName ?houseName . 
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, id));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/members/houses/{houseid:regex(^\w{8}$)}", Name = "ParliamentMembersHouse")]
        [HttpGet]
        public Graph MembersHouse(string parliamentid, string houseid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
        BIND(@parliamentid AS ?parliament)
        BIND(@houseid AS ?house)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?parliamentStartDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        ?house
            a :House ;
            :houseName ?houseName .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?person ;
                            :incumbencyStartDate ?incStartDate ;
                            :seatIncumbencyHasHouseSeat ?houseSeat .
            ?houseSeat :houseSeatHasHouse ?house .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup .
            ?constituencyGroup :constituencyGroupName ?constituencyName .

            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .

                ?person :partyMemberHasPartyMembership ?partyMembership .
                ?partyMembership :partyMembershipHasParty ?party ;
                                 :partyMembershipStartDate ?pmStartDate .
                OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               
                ?party :partyName ?partyName .

                BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
                BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
                FILTER (
                    (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
                    (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
                )
          }
       }
    }
    UNION {
        SELECT DISTINCT ?firstLetter WHERE {
          BIND(@parliamentid AS ?parliament)
		  BIND(@houseid AS ?house)
            
          ?parliament a :ParliamentPeriod .
          ?house a :House .
       	  ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
          ?seatIncumbency :incumbencyHasMember ?person ;
          				  :seatIncumbencyHasHouseSeat ?houseSeat .
          ?houseSeat :houseSeatHasHouse ?house .
          ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
          BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }
      }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("houseid", new Uri(BaseController.instance, houseid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/members/houses/{houseid:regex(^\w{8}$)}/a_z_letters", Name = "ParliamentMembersHouseAToZ")]
        [HttpGet]
        public Graph MembersHouseAToZLetters(string parliamentid, string houseid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    _:x :value ?firstLetter .
}
WHERE {
        SELECT DISTINCT ?firstLetter WHERE {
          BIND(@parliamentid AS ?parliament)
		  BIND(@houseid AS ?house)
            
          ?parliament a :ParliamentPeriod .
          ?house a :House .
       	  ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
          ?seatIncumbency :incumbencyHasMember ?person ;
          				  :seatIncumbencyHasHouseSeat ?houseSeat .
          ?houseSeat :houseSeatHasHouse ?house .
          ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
          BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("houseid", new Uri(BaseController.instance, houseid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/members/houses/{houseid:regex(^\w{8}$)}/{initial:regex(^\p{L}+$):maxlength(1)}", Name = "ParliamentMembersHouseByInitial")]
        [HttpGet]
        public Graph MembersHouseByInitial(string parliamentid, string houseid, string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
        BIND(@parliamentid AS ?parliament)
        BIND(@houseid AS ?house)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?parliamentStartDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        ?house
            a :House ;
            :houseName ?houseName .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?person ;
                            :incumbencyStartDate ?incStartDate ;
                            :seatIncumbencyHasHouseSeat ?houseSeat .
            ?houseSeat :houseSeatHasHouse ?house .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup .
            ?constituencyGroup :constituencyGroupName ?constituencyName .

            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .

            ?person :partyMemberHasPartyMembership ?partyMembership .
            ?partyMembership :partyMembershipHasParty ?party ;
                                :partyMembershipStartDate ?pmStartDate .
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               
            ?party :partyName ?partyName .

            BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
            BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
            FILTER (
                (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
                (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
            )
            FILTER STRSTARTS(LCASE(?listAs), LCASE(@initial))
          }
       }
    }
    UNION {
        SELECT DISTINCT ?firstLetter WHERE {
          BIND(@parliamentid AS ?parliament)
          BIND(@houseid AS ?house)
            
          ?parliament a :ParliamentPeriod.
          ?house a :House.
          ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
          ?seatIncumbency :incumbencyHasMember ?person ;
          				  :seatIncumbencyHasHouseSeat ?houseSeat.
          ?houseSeat :houseSeatHasHouse ?house .
          ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
          BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }
      }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("houseid", new Uri(BaseController.instance, houseid));
            query.SetLiteral("initial", initial);

            return BaseController.ExecuteList(query);
        }

        [Route(@"{id:regex(^\w{8}$)}/parties", Name = "ParliamentParties")]
        [HttpGet]
        public Graph Parties(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber ;
        :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	:parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?party
        a :Party ;
        :partyName ?partyName ;
        :count ?memberCount .
}
WHERE {
    SELECT ?parliament ?startDate ?endDate ?parliamentNumber ?party ?partyName ?nextParliament ?previousParliament (COUNT(?member) AS ?memberCount)
    WHERE {
        BIND(@parliamentid AS ?parliament)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?startDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?member ;
                            :incumbencyStartDate ?incStartDate .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?member :partyMemberHasPartyMembership ?partyMembership .
            ?partyMembership :partyMembershipHasParty ?party ;
        				     :partyMembershipStartDate ?pmStartDate .
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               
            ?party :partyName ?partyName .

            BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
            BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
            FILTER (
        	    (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	    (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		    )
        }
    }
    GROUP BY ?parliament ?startDate ?endDate ?parliamentNumber ?party ?partyName ?nextParliament ?previousParliament
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, id));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}", Name = "ParliamentParty")]
        [HttpGet]
        public Graph Party(string parliamentid, string partyid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber ;
        :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	:parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?party
        a :Party ;
        :partyName ?partyName ;
        :count ?memberCount .
}
WHERE {
    SELECT ?parliament ?startDate ?endDate ?parliamentNumber ?party ?partyName ?nextParliament ?previousParliament (COUNT(?member) AS ?memberCount)
    WHERE {
        BIND(@parliamentid AS ?parliament)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?startDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        OPTIONAL {
            BIND(@partyid AS ?party)
            ?party
                 a :Party ;
                 :partyName ?partyName .
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?member ;
                            :incumbencyStartDate ?incStartDate .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?member :partyMemberHasPartyMembership ?partyMembership .
            ?partyMembership :partyMembershipHasParty ?party ;
                             :partyMembershipStartDate ?pmStartDate .
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               

            BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
            BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
            FILTER (
                (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
                (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
            )
        }
    }
    GROUP BY ?parliament ?startDate ?endDate ?parliamentNumber ?party ?partyName ?nextParliament ?previousParliament
}";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/members", Name = "ParliamentPartyMembers")]
        [HttpGet]
        public Graph PartyMembers(string parliamentid, string partyid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
            BIND(@parliamentid AS ?parliament)
            BIND(@partyid AS ?party)
	?party
         a :Party ;
         :partyName ?partyName .
    ?parliament 
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?parliamentStartDate ;
        :parliamentPeriodNumber ?parliamentNumber .
    OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
    OPTIONAL {
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person ;
                        :incumbencyStartDate ?incStartDate ;
                        :seatIncumbencyHasHouseSeat ?houseSeat .
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            
        ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup ;
                    :houseSeatHasHouse ?house .
        ?house :houseName ?houseName .
        ?constituencyGroup :constituencyGroupName ?constituencyName .

        OPTIONAL { ?person :personGivenName ?givenName . }
        OPTIONAL { ?person :personFamilyName ?familyName . }
        OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
            
        ?person :partyMemberHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasParty ?party ;
        				 :partyMembershipStartDate ?pmStartDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               

        BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
        FILTER (
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
    }
   }
  }
    UNION {
        SELECT DISTINCT ?firstLetter WHERE {
             BIND(@parliamentid AS ?parliament)
             BIND(@partyid AS ?party)
        
            ?parliament a :ParliamentPeriod .
            ?party a :Party .
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?person ;
    					    :incumbencyStartDate ?incStartDate .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?person :partyMemberHasPartyMembership ?partyMembership .
            ?partyMembership :partyMembershipHasParty ?party ;
                             :partyMembershipStartDate ?pmStartDate .
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }   
    
            BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
            BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
            FILTER (
        	    (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	    (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		    )
            
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
            BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }

    }         
}";
            // Use @parliamentid, @houseid

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/members/a_z_letters", Name = "ParliamentPartyMembersAToZ")]
        [HttpGet]
        public Graph PartyMembersAToZLetters(string parliamentid, string partyid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    _:x :value ?firstLetter .
}
WHERE {
    SELECT DISTINCT ?firstLetter WHERE {
         BIND(@parliamentid AS ?parliament)
         BIND(@partyid AS ?party)
        
        ?parliament a :ParliamentPeriod .
        ?party a :Party .
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person ;
    					:incumbencyStartDate ?incStartDate .
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
        ?person :partyMemberHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasParty ?party ;
                         :partyMembershipStartDate ?pmStartDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }   
    
        BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
        FILTER (
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
            
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/members/{initial:regex(^\p{L}+$):maxlength(1)}", Name = "ParliamentPartyMembersByInitial")]
        [HttpGet]
        public Graph PartyMembersByInitial(string parliamentid, string partyid, string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
            BIND(@parliamentid AS ?parliament)
            BIND(@partyid AS ?party)
	?party
         a :Party ;
         :partyName ?partyName .
    ?parliament 
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?parliamentStartDate ;
        :parliamentPeriodNumber ?parliamentNumber .
    OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
   	OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }        
    OPTIONAL {
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person ;
                        :incumbencyStartDate ?incStartDate ;
                        :seatIncumbencyHasHouseSeat ?houseSeat .
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            
            ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup ;
                       :houseSeatHasHouse ?house .
            ?house :houseName ?houseName .
            ?constituencyGroup :constituencyGroupName ?constituencyName .

            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
            
        ?person :partyMemberHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasParty ?party ;
        				 :partyMembershipStartDate ?pmStartDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }               

        BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
        FILTER (
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)    
        FILTER STRSTARTS(LCASE(?listAs), LCASE(@initial))        
      }   
     }
    }
    UNION {
        SELECT DISTINCT ?firstLetter WHERE {
         BIND(@parliamentid AS ?parliament)
         BIND(@partyid AS ?party)
        
        ?parliament a :ParliamentPeriod.
        ?party a :Party.
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person ;
    					:incumbencyStartDate ?incStartDate.
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
        ?person :partyMemberHasPartyMembership ?partyMembership.
        ?partyMembership :partyMembershipHasParty ?party ;
                         :partyMembershipStartDate ?pmStartDate.
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

        BIND(COALESCE(?partyMembershipEndDate, now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate, now()) AS ?incEndDate)
        FILTER(
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
            
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }
    }         
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));
            query.SetLiteral("initial", initial);

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/houses", Name = "ParliamentPartyHouses")]
        [HttpGet]
        public Graph PartyHouses(string parliamentid, string partyid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?party
        a :Party ;
        :partyName ?partyName .
    ?house
        a :House ;
        :houseName ?houseName .
}
WHERE {
        BIND(@parliamentid AS ?parliament)
    	BIND(@partyid AS ?party)
    	?party
        	a :Party ;
         	:partyName ?partyName .
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?startDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
   	    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
    	
    OPTIONAL {
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?member ;
                        :incumbencyStartDate ?incStartDate ;
                        :seatIncumbencyHasHouseSeat ?houseSeat .
        ?houseSeat :houseSeatHasHouse ?house .
        ?house :houseName ?houseName .
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
        ?member :partyMemberHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasParty ?party ;
        				 :partyMembershipStartDate ?pmStartDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

        BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
        FILTER (
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/houses/{houseid:regex(^\w{8}$)}", Name = "ParliamentPartyHouse")]
        [HttpGet]
        public Graph PartyHouse(string parliamentid, string partyid, string houseid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?party
        a :Party ;
        :partyName ?partyName .
    ?house
        a :House ;
        :houseName ?houseName .
}
WHERE {
        BIND(@parliamentid AS ?parliament)
        BIND(@partyid AS ?party)

    	?party
        	a :Party ;
         	:partyName ?partyName .
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?startDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
   	    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
    	
    OPTIONAL {
        BIND(@houseid AS ?house)

    	?house
        	a :House ;
         	:houseName ?houseName .
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?member ;
                        :incumbencyStartDate ?incStartDate ;
                        :seatIncumbencyHasHouseSeat ?houseSeat .
        ?houseSeat :houseSeatHasHouse ?house .
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
        ?member :partyMemberHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasParty ?party ;
        				 :partyMembershipStartDate ?pmStartDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

        BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
        FILTER (
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));
            query.SetUri("houseid", new Uri(BaseController.instance, partyid));

            return BaseController.ExecuteSingle(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/houses/{houseid:regex(^\w{8}$)}/members", Name = "ParliamentPartyHouseMembers")]
        [HttpGet]
        public Graph PartyHouseMembers(string parliamentid, string partyid, string houseid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
        BIND(@parliamentid AS ?parliament)
        BIND(@partyid AS ?party)
        BIND(@houseid AS ?house)

    	?party
        	a :Party ;
         	:partyName ?partyName .
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?parliamentStartDate ;
            :parliamentPeriodNumber ?parliamentNumber .
    	?house
        	a :House ;
         	:houseName ?houseName .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
   	    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
    	
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?person ;
                            :incumbencyStartDate ?incStartDate ;
                            :seatIncumbencyHasHouseSeat ?houseSeat .
            ?houseSeat :houseSeatHasHouse ?house ;
                	    :houseSeatHasConstituencyGroup ?constituencyGroup .
            ?constituencyGroup :constituencyGroupName ?constituencyName .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?person :partyMemberHasPartyMembership ?partyMembership .
            ?partyMembership :partyMembershipHasParty ?party ;
        				     :partyMembershipStartDate ?pmStartDate .
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

            BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
            BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
            FILTER (
        	    (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	    (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		    )
            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        }
      }
    }
    UNION {
        SELECT DISTINCT ?firstLetter WHERE {
            BIND(@parliamentid AS ?parliament)
            BIND(@partyid AS ?party)
            BIND(@houseid AS ?house)
		
            ?party a :Party .
            ?house a :House .
            ?parliament a :ParliamentPeriod ;                
        			    :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :incumbencyHasMember ?person ;
                            :seatIncumbencyHasHouseSeat ?houseSeat ;
            			    :incumbencyStartDate ?incStartDate.
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
            ?houseSeat :houseSeatHasHouse ?house .
            ?person :partyMemberHasPartyMembership ?partyMembership.
            ?partyMembership :partyMembershipHasParty ?party ;
                             :partyMembershipStartDate ?pmStartDate.
            OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

            BIND(COALESCE(?partyMembershipEndDate, now()) AS ?pmEndDate)
            BIND(COALESCE(?seatIncumbencyEndDate, now()) AS ?incEndDate)
            FILTER(
        	    (?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	    (?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		    )
            
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
            BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));
            query.SetUri("houseid", new Uri(BaseController.instance, partyid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/houses/{houseid:regex(^\w{8}$)}/members/a_z_letters", Name = "ParliamentPartyHouseMembersAToZ")]
        [HttpGet]
        public Graph PartyHouseMembersAToZLetters(string parliamentid, string partyid, string houseid)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
	_:x :value ?firstLetter .
}
WHERE {
   SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)
        BIND(@partyid AS ?party)
        BIND(@houseid AS ?house)
		
        ?party a :Party .
        ?house a :House .
        ?parliament a :ParliamentPeriod ;                
        			:parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person ;
                        :seatIncumbencyHasHouseSeat ?houseSeat ;
            			:incumbencyStartDate ?incStartDate.
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
        ?houseSeat :houseSeatHasHouse ?house .
        ?person :partyMemberHasPartyMembership ?partyMembership.
        ?partyMembership :partyMembershipHasParty ?party ;
                         :partyMembershipStartDate ?pmStartDate.
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

        BIND(COALESCE(?partyMembershipEndDate, now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate, now()) AS ?incEndDate)
        FILTER(
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
            
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
   }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));
            query.SetUri("houseid", new Uri(BaseController.instance, partyid));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{parliamentid:regex(^\w{8}$)}/parties/{partyid:regex(^\w{8}$)}/houses/{houseid:regex(^\w{8}$)}/members/{initial:regex(^\p{L}+$):maxlength(1)}", Name = "ParliamentPartyHouseMembersByInitial")]
        [HttpGet]
        public Graph PartyHouseMembersByInitial(string parliamentid, string partyid, string houseid, string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs ;
        :memberHasIncumbency ?seatIncumbency ;
        :partyMemberHasPartyMembership ?partyMembership .
   ?seatIncumbency
        a :SeatIncumbency ;
        :seatIncumbencyHasHouseSeat ?houseSeat ;
        :incumbencyEndDate ?seatIncumbencyEndDate .   
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasHouse ?house ;
        :houseSeatHasConstituencyGroup ?constituencyGroup .
   ?constituencyGroup
        a :ConstituencyGroup;
        :constituencyGroupName ?constituencyName .
    ?partyMembership
        a :PartyMembership ;
        :partyMembershipHasParty ?party ;
        :partyMembershipEndDate ?partyMembershipEndDate .
    ?party
        a :Party ;
        :partyName ?partyName .
     ?parliament 
         a :ParliamentPeriod ;
         :parliamentPeriodStartDate ?parliamentStartDate ;
         :parliamentPeriodEndDate ?parliamentEndDate ;
         :parliamentPeriodNumber ?parliamentNumber ;
         :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	 :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?house
        a :House ;
        :houseName ?houseName .
    _:x :value ?firstLetter .
}
WHERE {
    { SELECT * WHERE {
        BIND(@parliamentid AS ?parliament)
        BIND(@partyid AS ?party)
        BIND(@houseid AS ?house)

    	?party
        	a :Party ;
         	:partyName ?partyName .
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?parliamentStartDate ;
            :parliamentPeriodNumber ?parliamentNumber .
    	?house
        	a :House ;
         	:houseName ?houseName .
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?parliamentEndDate . }
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
   	    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
    	
    OPTIONAL {
        ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :incumbencyHasMember ?person ;
                        :incumbencyStartDate ?incStartDate ;
                        :seatIncumbencyHasHouseSeat ?houseSeat .
        ?houseSeat :houseSeatHasHouse ?house ;
                	:houseSeatHasConstituencyGroup ?constituencyGroup .
        ?constituencyGroup :constituencyGroupName ?constituencyName .
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
        ?person :partyMemberHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasParty ?party ;
        				 :partyMembershipStartDate ?pmStartDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

        BIND(COALESCE(?partyMembershipEndDate,now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate,now()) AS ?incEndDate)
        FILTER (
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
        OPTIONAL { ?person :personGivenName ?givenName . }
        OPTIONAL { ?person :personFamilyName ?familyName . }
        OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .

        FILTER STRSTARTS(LCASE(?listAs), LCASE(@initial))     
    }
   }
    }
    UNION {
        SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)
        BIND(@partyid AS ?party)
        BIND(@houseid AS ?house)

        ?party a :Party.
        ?house a :House.
        ?parliament a :ParliamentPeriod ;                
        			:parliamentPeriodHasSeatIncumbency ?seatIncumbency.
        ?seatIncumbency :incumbencyHasMember ?person ;
                        :seatIncumbencyHasHouseSeat ?houseSeat;
            			:incumbencyStartDate ?incStartDate.
        OPTIONAL { ?seatIncumbency :incumbencyEndDate ?seatIncumbencyEndDate . }
        ?houseSeat :houseSeatHasHouse ?house.
        ?person :partyMemberHasPartyMembership ?partyMembership.
        ?partyMembership :partyMembershipHasParty ?party ;
                         :partyMembershipStartDate ?pmStartDate.
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?partyMembershipEndDate . }

        BIND(COALESCE(?partyMembershipEndDate, now()) AS ?pmEndDate)
        BIND(COALESCE(?seatIncumbencyEndDate, now()) AS ?incEndDate)
        FILTER(
        	(?pmStartDate <= ?incStartDate && ?pmEndDate > ?incStartDate) ||
        	(?pmStartDate >= ?incStartDate && ?pmStartDate < ?incEndDate)
		)
            
        ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        BIND(ucase(SUBSTR(?listAs, 1, 1)) as ?firstLetter)
        }
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, parliamentid));
            query.SetUri("partyid", new Uri(BaseController.instance, partyid));
            query.SetUri("houseid", new Uri(BaseController.instance, partyid));
            query.SetLiteral("initial", initial);

            return BaseController.ExecuteList(query);
        }

        [Route(@"{id:regex(^\w{8}$)}/constituencies", Name = "ParliamentConstituencies")]
        [HttpGet]
        public Graph Constituencies(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber ;
        :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	:parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?constituencyGroup
        a :ConstituencyGroup ;
        :constituencyGroupName ?constituencyGroupName ;
        :constituencyGroupHasHouseSeat ?houseSeat .
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasSeatIncumbency ?seatIncumbency .
    ?seatIncumbency
        a :SeatIncumbency ;
        :incumbencyHasMember ?person ;
        :incumbencyStartDate ?incStartDate ;
        :incumbencyEndDate ?incEndDate .
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
}
WHERE {
    { SELECT * WHERE {
    BIND(@parliamentid AS ?parliament)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?startDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
   	    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :seatIncumbencyHasHouseSeat ?houseSeat ;
                            :incumbencyHasMember ?person ;
                            :incumbencyStartDate ?incStartDate .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?incEndDate . }
            ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup .
            ?constituencyGroup :constituencyGroupName ?constituencyGroupName .

            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
        }
      }
    }
    UNION {
      SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)

        ?parliament a :ParliamentPeriod ;
                    :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :seatIncumbencyHasHouseSeat ?houseSeat .
        ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup .
        ?constituencyGroup :constituencyGroupName ?constituencyGroupName .

          BIND(ucase(SUBSTR(?constituencyGroupName, 1, 1)) as ?firstLetter)
        }
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, id));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{id:regex(^\w{8}$)}/constituencies/a_z_letters", Name = "ParliamentConstituenciesAToZ")]
        [HttpGet]
        public Graph ConstituenciesAToZLetters(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
  _:x :value ?firstLetter .
}
WHERE {
    SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)

        ?parliament a :ParliamentPeriod ;
                    :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :seatIncumbencyHasHouseSeat ?houseSeat .
        ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup .
        ?constituencyGroup :constituencyGroupName ?constituencyGroupName .
        BIND(ucase(SUBSTR(?constituencyGroupName, 1, 1)) as ?firstLetter)
    } 
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, id));

            return BaseController.ExecuteList(query);
        }

        [Route(@"{id:regex(^\w{8}$)}/constituencies/{initial:regex(^\p{L}+$):maxlength(1)}", Name = "ParliamentConstituenciesByInitial")]
        [HttpGet]
        public Graph ConstituenciesByInitial(string id, string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
CONSTRUCT {
    ?parliament
        a :ParliamentPeriod ;
        :parliamentPeriodStartDate ?startDate ;
        :parliamentPeriodEndDate ?endDate ;
        :parliamentPeriodNumber ?parliamentNumber ;
        :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament ;
    	:parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament .
    ?constituencyGroup
        a :ConstituencyGroup ;
        :constituencyGroupName ?constituencyGroupName ;
        :constituencyGroupHasHouseSeat ?houseSeat .
    ?houseSeat
        a :HouseSeat ;
        :houseSeatHasSeatIncumbency ?seatIncumbency .
    ?seatIncumbency
        a :SeatIncumbency ;
        :incumbencyHasMember ?person ;
        :incumbencyStartDate ?incStartDate ;
        :incumbencyEndDate ?incEndDate .
    ?person
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs ;
        <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .

}
WHERE {
    { SELECT * WHERE {
    BIND(@parliamentid AS ?parliament)
        ?parliament 
            a :ParliamentPeriod ;
            :parliamentPeriodStartDate ?startDate ;
            :parliamentPeriodNumber ?parliamentNumber .
        OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyFollowingParliamentPeriod ?nextParliament . }
   	    OPTIONAL { ?parliament :parliamentPeriodHasImmediatelyPreviousParliamentPeriod ?previousParliament . }
        OPTIONAL { ?parliament :parliamentPeriodEndDate ?endDate . }
        OPTIONAL {
            ?parliament :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
            ?seatIncumbency :seatIncumbencyHasHouseSeat ?houseSeat ;
                            :incumbencyHasMember ?person ;
                            :incumbencyStartDate ?incStartDate .
            OPTIONAL { ?seatIncumbency :incumbencyEndDate ?incEndDate . }
            ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup .
            ?constituencyGroup :constituencyGroupName ?constituencyGroupName .

            OPTIONAL { ?person :personGivenName ?givenName . }
            OPTIONAL { ?person :personFamilyName ?familyName . }
            OPTIONAL { ?person <http://example.com/F31CBD81AD8343898B49DC65743F0BDF> ?displayAs } .
            ?person <http://example.com/A5EE13ABE03C4D3A8F1A274F57097B6C> ?listAs .
            FILTER STRSTARTS(LCASE(?constituencyGroupName), LCASE(@initial))
        }
      }
    }
    UNION {
      SELECT DISTINCT ?firstLetter WHERE {
        BIND(@parliamentid AS ?parliament)

        ?parliament a :ParliamentPeriod.
                    :parliamentPeriodHasSeatIncumbency ?seatIncumbency .
        ?seatIncumbency :seatIncumbencyHasHouseSeat ?houseSeat .
        ?houseSeat :houseSeatHasConstituencyGroup ?constituencyGroup .
        ?constituencyGroup :constituencyGroupName ?constituencyGroupName .

          BIND(ucase(SUBSTR(?constituencyGroupName, 1, 1)) as ?firstLetter)
        }
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("parliamentid", new Uri(BaseController.instance, id));
            query.SetLiteral("initial", initial);

            return BaseController.ExecuteList(query);
        }
    }
}
