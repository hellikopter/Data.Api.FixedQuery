﻿namespace Parliament.Data.Api.FixedQuery.Controllers
{
    using System;
    using System.Net.Http;
    using System.Web.Http;
    using VDS.RDF;
    using VDS.RDF.Query;

    [RoutePrefix("parties")]
    public class PartyController : BaseController
    {
        // Ruby route: resources :parties, only: [:index] 
        [Route("", Name = "PartyIndex")]
        [HttpGet]
        public HttpResponseMessage Index()
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party
        a :Party ;
        :partyName ?partyName .
    }
WHERE {
    ?party
        a :Party ;
        :partyHasPartyMembership ?partyMembership ;
        :partyName ?partyName .
}
";

            var query = new SparqlParameterizedString(queryString);

            return Execute(query);
        }
        // Ruby route: match '/parties/:party', to: 'parties#show', party: /\w{8}-\w{4}-\w{4}-\w{4}-\w{12}/, via: [:get]
        [Route("{id:guid}", Name = "PartyById")]
        [HttpGet]
        public HttpResponseMessage ById(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party
        a :Party ;
        :partyName ?name .
}
WHERE {
    BIND(@id AS ?party)

    ?party :partyName ?name .
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("id", new Uri(BaseController.instance, id));

            return Execute(query);

        }

        // Ruby route: get '/parties/:letter', to: 'parties#letters', letter: /[A-Za-z]/, via: [:get]
        [Route(@"{initial:regex(^\p{L}+$):maxlength(1)}", Name = "PartyByInitial")]
        [HttpGet]
        public HttpResponseMessage ByInitial(string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party
        a :Party ;
        :partyName ?partyName .
}
WHERE {
    ?party
        a :Party ;
        :partyName ?partyName .

    FILTER STRSTARTS(LCASE(?partyName), LCASE(@letter)) .
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetLiteral("letter", initial);

            return Execute(query);
        }

        // Ruby route: get '/parties/current', to: 'parties#current'
        [Route("current", Name = "PartyCurrent")]
        [HttpGet]
        public HttpResponseMessage Current() {
            var querystring = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party 
        a :Party ;
        :partyName ?partyName .
}
WHERE {
    ?incumbency :incumbencyHasMember ?member .
    FILTER NOT EXISTS { ?incumbency a :PastIncumbency . }
    ?member :partyMemberHasPartyMembership ?partyMembership .
    FILTER NOT EXISTS { ?partyMembership a :PastPartyMembership . }
    ?partyMembership :partyMembershipHasParty ?party .
    ?party :partyName ?partyName .
}
";

            return Execute(querystring);
        }

        // Ruby route: get '/parties/a_z_letters', to: 'parties#a_z_letters_all'
        [Route("a_z_letters", Name = "PartyAToZ")]
        [HttpGet]
        public HttpResponseMessage AToZLetters()
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
     _:x :value ?firstLetter.
}
WHERE {
    SELECT DISTINCT ?firstLetter WHERE {
    ?party :partyName ?partyName .
    BIND(ucase(SUBSTR(?partyName, 1, 1)) as ?firstLetter)
    }
}
";

            var query = new SparqlParameterizedString(queryString);
            return Execute(query);
        }

        // Ruby route: get '/parties/current/a_z_letters', to: 'parties#a_z_letters_current'
        // NOTE: this returns parties who currently have members in parliament, not parties currently active or seeking election
        // ALSO NOTE: mnis thinks Bishops are a party
        [Route("current/a_z_letters", Name = "PartyCurrentAToZ")]
        [HttpGet]
        public HttpResponseMessage CurrentAToZParties()
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
     _:x :value ?firstLetter.
}
WHERE {
    SELECT DISTINCT ?firstLetter WHERE {
    ?incumbency :incumbencyHasMember ?member .
    FILTER NOT EXISTS { ?incumbency a :PastIncumbency . }
    ?member :partyMemberHasPartyMembership ?partyMembership .
    FILTER NOT EXISTS { ?partyMembership a :PastPartyMembership . }
    ?partyMembership :partyMembershipHasParty ?party .
    ?party :partyName ?partyName .
    BIND(ucase(SUBSTR(?partyName, 1, 1)) as ?firstLetter)
    }
}
";

            var query = new SparqlParameterizedString(queryString);
            return Execute(query);
        }

        // Ruby route: get '/parties/lookup', to: 'parties#lookup'
        [Route(@"lookup/{source:regex(^\p{L}+$)}/{id}", Name = "PartyLookup")]
        [HttpGet]
        public HttpResponseMessage Lookup(string source, string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party a :Party .
}
WHERE {
    BIND(@id AS ?id)
    BIND(@source AS ?source)

    ?party
        a :Party ;
        ?source ?id .
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("source", new Uri(BaseController.schema, source));
            query.SetLiteral("id", id);

            return Execute(query);
        }
        // Ruby route: get '/parties/:letters', to: 'parties#lookup_by_letters'
        [Route(@"{letters:regex(^\p{L}+$):minlength(2)}", Name = "PartyByLetters")]
        [HttpGet]
        public HttpResponseMessage ByLetters(string letters)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party 
        a :Party;
        :partyName ?partyName .
}
WHERE {
    ?party a :Party.
    ?party :partyName ?partyName .

    FILTER CONTAINS(LCASE(?partyName), LCASE(@letters))
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetLiteral("letters", letters);

            return Execute(query);
        }

        // Ruby route: resources :parties, only: [:index] do get '/members', to: 'parties#members' end
        [Route("{id:guid}/members", Name = "PartyMembers")]
        [HttpGet]
        public HttpResponseMessage Members(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>
      
CONSTRUCT {
    ?party 
        a :Party ;
        :partyName ?partyName .
    ?person 
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        :partyMemberHasPartyMembership ?partyMembership .
    ?partyMembership a :PartyMembership ;
        :partyMembershipStartDate ?startDate ;
        :partyMembershipEndDate ?endDate .
    }
WHERE {
    BIND(@partyid AS ?party)
   	?party :partyName ?partyName .
    OPTIONAL {
        ?party :partyHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasPartyMember ?person .
        ?partyMembership :partyMembershipStartDate ?startDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?endDate . }
        OPTIONAL { ?person :personGivenName ?givenName . }
        OPTIONAL { ?person :personFamilyName ?familyName . }
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("partyid", new Uri(BaseController.instance, id));

            return Execute(query);
        }

        // Ruby route: resources :parties, only: [:index] do get '/members/current', to: 'parties#current_members' end
        [Route("{id:guid}/members/current", Name = "PartyCurrentMembers")]
        [HttpGet]
        public HttpResponseMessage CurrentMembers(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party 
        a :Party ;
        :partyName ?partyName .
    ?person 
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        :partyMemberHasPartyMembership ?partyMembership .
    ?partyMembership 
        a :PartyMembership ;
        :partyMembershipStartDate ?startDate .
    }
WHERE {
    BIND(@partyid AS ?party)
   	?party :partyName ?partyName .
    OPTIONAL {
        ?party :partyHasPartyMembership ?partyMembership .
        FILTER NOT EXISTS { ?partyMembership a :PastPartyMembership . }
        ?partyMembership :partyMembershipHasPartyMember ?person .
        ?person :memberHasIncumbency ?incumbency .
        FILTER NOT EXISTS { ?incumbency a :PastIncumbency . }
        ?partyMembership :partyMembershipStartDate ?startDate .
        OPTIONAL { ?person :personGivenName ?givenName . }
        OPTIONAL { ?person :personFamilyName ?familyName . }
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("partyid", new Uri(BaseController.instance, id));

            return Execute(query);
        }
        // Ruby route: resources :parties, only: [:index] do match '/members/:letter', to: 'parties#members_letters', letter: /[A-Za-z]/, via: [:get] end
        [Route(@"{id:guid}/members/{initial:regex(^\p{L}+$):maxlength(1)}", Name = "PartyMembersByInitial")]
        [HttpGet]
        public HttpResponseMessage MembersByInitial(string id, string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    ?party 
        a :Party ;
        :partyName ?partyName .
    ?person 
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        :partyMemberHasPartyMembership ?partyMembership .
    ?partyMembership a :PartyMembership ;
        :partyMembershipStartDate ?startDate ;
        :partyMembershipEndDate ?endDate .
    }
WHERE {
    BIND(@partyid AS ?party)
   	?party :partyName ?partyName .
    OPTIONAL {
        ?party :partyHasPartyMembership ?partyMembership .
        ?partyMembership :partyMembershipHasPartyMember ?person .
        ?partyMembership :partyMembershipStartDate ?startDate .
        OPTIONAL { ?partyMembership :partyMembershipEndDate ?endDate . }
        OPTIONAL { ?person :personGivenName ?givenName . }
        OPTIONAL { ?person :personFamilyName ?familyName . }
        FILTER STRSTARTS(LCASE(?familyName), LCASE(@initial))
        }
    }
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("partyid", new Uri(BaseController.instance, id));
            query.SetLiteral("initial", initial);

            return Execute(query);
        }

        // Ruby route: resources :parties, only: [:index] do get '/members/a_z_letters', to: 'parties#a_z_letters_members' end
        [Route("{id:guid}/members/a_z_letters", Name = "PartyMembersAToZ")]
        [HttpGet]
        public HttpResponseMessage MembersAToZLetters(string id)
        {
            var queryString = @"
      PREFIX : <http://id.ukpds.org/schema/>
      CONSTRUCT {
         _:x :value ?firstLetter .
      }
      WHERE {
        SELECT DISTINCT ?firstLetter WHERE {
          BIND(@partyid AS ?party)

	        ?party :partyHasPartyMembership ?partyMembership .
          FILTER NOT EXISTS { ?partyMembership a :PastPartyMembership . }
          ?partyMembership :partyMembershipHasPartyMember ?person .
          ?person :memberHasIncumbency ?incumbency .
          FILTER NOT EXISTS { ?incumbency a :PastIncumbency . }
          ?person :personFamilyName ?familyName .

          BIND(ucase(SUBSTR(?familyName, 1, 1)) as ?firstLetter)
        }
      }
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("partyid", new Uri(BaseController.instance, id));

            return Execute(query);
        }


        // Ruby route: resources :parties, only: [:index] do match '/members/current/:letter', to: 'parties#current_members_letters', letter: /[A-Za-z]/, via: [:get] end
        [Route(@"{id:guid}/members/current/{initial:regex(^\p{L}+$):maxlength(1)}", Name = "PartyCurrentMembersByInitial")]
        [HttpGet]
        public HttpResponseMessage CurrentMembersByInitial(string id, string initial)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
   ?party
        a :Party ;
        :partyName ?partyName .
    ?person 
        a :Person ;
        :personGivenName ?givenName ;
        :personFamilyName ?familyName ;
        :partyMemberHasPartyMembership ?partyMembership .
    ?partyMembership 
        a :PartyMembership ;
        :partyMembershipStartDate ?startDate .
    }
WHERE {
    BIND(@partyid AS ?party)
   	?party :partyName ?partyName .
    OPTIONAL {
        ?party :partyHasPartyMembership ?partyMembership .
        FILTER NOT EXISTS { ?partyMembership a :PastPartyMembership . }
        ?partyMembership :partyMembershipHasPartyMember ?person .
        ?person :memberHasIncumbency ?incumbency .
        FILTER NOT EXISTS { ?incumbency a :PastIncumbency . }
        ?partyMembership :partyMembershipStartDate ?startDate .
        OPTIONAL { ?person :personGivenName ?givenName . }
        OPTIONAL { ?person :personFamilyName ?familyName . }
        }
    FILTER STRSTARTS(LCASE(?familyName), LCASE(@initial))
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("partyid", new Uri(BaseController.instance, id));
            query.SetLiteral("initial", initial);

            return Execute(query);
        }
        // Ruby route: resources :parties, only: [:index] do get '/members/current/a_z_letters', to: 'parties#a_z_letters_members_current' end
        [Route("{id:guid}/members/current/a_z_letters", Name = "PartyCurrentMembersAToZ")]
        [HttpGet]
        public HttpResponseMessage CurrentMembersAToZLetters(string id)
        {
            var queryString = @"
PREFIX : <http://id.ukpds.org/schema/>

CONSTRUCT {
    _:x :value ?firstLetter .
    }
WHERE {
    SELECT DISTINCT ?firstLetter WHERE {
        BIND(@partyid AS ?party)
        ?party :partyHasPartyMembership ?partyMembership .
        FILTER NOT EXISTS { ?partyMembership a :PastPartyMembership . }
        ?partyMembership :partyMembershipHasPartyMember ?person .
        ?person :memberHasIncumbency ?incumbency .
        FILTER NOT EXISTS { ?incumbency a :PastIncumbency . }
        ?person :personFamilyName ?familyName .
        BIND(ucase(SUBSTR(?familyName, 1, 1)) as ?firstLetter)
    }
}
";

            var query = new SparqlParameterizedString(queryString);

            query.SetUri("partyid", new Uri(BaseController.instance, id));

            return Execute(query);
        }
    }
}